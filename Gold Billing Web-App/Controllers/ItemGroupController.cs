using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemGroupController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ItemGroupController> _logger;

        public ItemGroupController(AppDbContext context, ILogger<ItemGroupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                _logger.LogWarning("User is not authenticated or UserId is invalid.");
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        public async Task<IActionResult> ItemGroupList()
        {
            var userId = GetCurrentUserId();
            var itemGroups = await _context.ItemGroups
                .Where(ig => ig.UserId == userId)
                .ToListAsync();

            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Date", typeof(DateTime));
            foreach (var group in itemGroups)
            {
                table.Rows.Add(group.Id, group.Name, group.Date);
            }
            return View(table);
        }

        public IActionResult AddEditItemGroup(int? Id)
        {
            var userId = GetCurrentUserId();
            ItemGroupModel itemGroupModel = new ItemGroupModel { UserId = userId };

            if (Id.HasValue && Id > 0)
            {
                itemGroupModel = _context.ItemGroups
                    .FirstOrDefault(ig => ig.Id == Id && ig.UserId == userId);
                if (itemGroupModel == null)
                {
                    _logger.LogWarning("Item group not found for Id: {Id} and UserId: {UserId}", Id, userId);
                    TempData["ErrorMessage"] = "Item group not found or you do not have access to it.";
                    return RedirectToAction("ItemGroupList");
                }
            }
            return View(itemGroupModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItemGroup(ItemGroupModel itemGroup)
        {
            var userId = GetCurrentUserId();
            itemGroup.UserId = userId;

            // Clear validation errors for navigation properties
            if (ModelState.ContainsKey("User"))
            {
                ModelState.Remove("User");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ModelState invalid for ItemGroup. Errors: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join(", ", errors);
                return View("AddEditItemGroup", itemGroup);
            }

            try
            {
                // Verify that the UserId exists in the Users table
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogError("UserId {UserId} does not exist in the Users table.", userId);
                    TempData["ErrorMessage"] = "Invalid user. Please log in again.";
                    return View("AddEditItemGroup", itemGroup);
                }

                itemGroup.Date = DateTime.Now;
                if (!itemGroup.Id.HasValue || itemGroup.Id == 0) // Insert
                {
                    _context.ItemGroups.Add(itemGroup);
                    _logger.LogInformation("New item group added for UserId: {UserId}, Name: {Name}", userId, itemGroup.Name);
                    TempData["SuccessMessage"] = "Item group added successfully!";
                }
                else // Update
                {
                    var existingGroup = await _context.ItemGroups
                        .FirstOrDefaultAsync(ig => ig.Id == itemGroup.Id && ig.UserId == userId);
                    if (existingGroup == null)
                    {
                        _logger.LogWarning("Item group not found for Id: {Id} and UserId: {UserId}", itemGroup.Id, userId);
                        TempData["ErrorMessage"] = "Item group not found or you do not have access to it.";
                        return RedirectToAction("ItemGroupList");
                    }

                    existingGroup.Name = itemGroup.Name;
                    existingGroup.Date = itemGroup.Date;
                    _context.ItemGroups.Update(existingGroup);
                    _logger.LogInformation("Item group updated for Id: {Id}, UserId: {UserId}", itemGroup.Id, userId);
                    TempData["SuccessMessage"] = "Item group updated successfully!";
                }

                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for ItemGroup Id: {Id}", itemGroup.Id);
                    TempData["ErrorMessage"] = "No changes were saved to the database.";
                }
                return RedirectToAction("ItemGroupList");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item group for UserId: {UserId}", userId);
                TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                return View("AddEditItemGroup", itemGroup);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItemGroup(int id)
        {
            var userId = GetCurrentUserId();

            try
            {
                var itemGroup = await _context.ItemGroups
                    .FirstOrDefaultAsync(ig => ig.Id == id && ig.UserId == userId);

                if (itemGroup == null)
                {
                    _logger.LogWarning("DeleteItemGroup - Item group not found for Id: {Id} and UserId: {UserId}", id, userId);
                    return Json(new { success = false, error = "Item group not found or you do not have access to it." });
                }

                // Check for dependent items
                var hasItems = await _context.Items
                    .AnyAsync(i => i.ItemGroupId == id);

                if (hasItems)
                {
                    _logger.LogWarning("DeleteItemGroup - Cannot delete item group with associated items. Id: {Id}", id);
                    return Json(new { success = false, error = "This item group has associated items and cannot be deleted." });
                }

                _context.ItemGroups.Remove(itemGroup);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Item group deleted successfully for Id: {Id}, UserId: {UserId}", id, userId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item group for Id: {Id}, UserId: {UserId}", id, userId);
                return Json(new { success = false, error = $"Error deleting item group: {ex.Message}" });
            }
        }
    }
}