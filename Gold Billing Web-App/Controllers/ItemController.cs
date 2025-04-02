using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ItemController> _logger;

        public ItemController(AppDbContext context, ILogger<ItemController> logger)
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

        public List<ItemGroupDropDownModel> SetItemGroupDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.ItemGroups
                .Where(g => g.UserId == userId)
                .Select(g => new ItemGroupDropDownModel
                {
                    Id = (int)g.Id!,
                    GroupName = g.Name
                })
                .ToList();
        }

        public async Task<IActionResult> ItemList()
        {
            var userId = GetCurrentUserId();
            var items = await _context.Items
                .Where(i => i.UserId == userId)
                .ToListAsync();

            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("ItemGroupId", typeof(int));
            table.Columns.Add("GroupName", typeof(string));
            foreach (var item in items)
            {
                var itemGroup = await _context.ItemGroups.FirstOrDefaultAsync(ig => ig.Id == item.ItemGroupId);
                table.Rows.Add(item.Id, item.Name, item.ItemGroupId, itemGroup?.Name);
            }
            return View(table);
        }

        public IActionResult AddEditItem(int? Id)
        {
            var userId = GetCurrentUserId();
            ViewBag.ItemGroupList = SetItemGroupDropDown();
            ItemModel item = new ItemModel { UserId = userId };

            if (Id.HasValue && Id > 0)
            {
                item = _context.Items
                    .FirstOrDefault(i => i.Id == Id && i.UserId == userId);
                if (item == null)
                {
                    _logger.LogWarning("Item not found for Id: {Id} and UserId: {UserId}", Id, userId);
                    TempData["ErrorMessage"] = "Item not found or you do not have access to it.";
                    return RedirectToAction("ItemList");
                }
            }
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItem(ItemModel item)
        {
            var userId = GetCurrentUserId();
            item.UserId = userId;

            // Clear validation errors for navigation properties
            if (ModelState.ContainsKey("ItemGroup"))
            {
                ModelState.Remove("ItemGroup");
            }
            if (ModelState.ContainsKey("User"))
            {
                ModelState.Remove("User");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ModelState invalid for Item. Errors: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join(", ", errors);
                ViewBag.ItemGroupList = SetItemGroupDropDown();
                return View("AddEditItem", item);
            }

            try
            {
                // Verify that the UserId exists in the Users table
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogError("UserId {UserId} does not exist in the Users table.", userId);
                    TempData["ErrorMessage"] = "Invalid user. Please log in again.";
                    ViewBag.ItemGroupList = SetItemGroupDropDown();
                    return View("AddEditItem", item);
                }

                // Verify that the ItemGroupId exists and belongs to the current user
                var itemGroupExists = await _context.ItemGroups.AnyAsync(ig => ig.Id == item.ItemGroupId && ig.UserId == userId);
                if (!itemGroupExists)
                {
                    _logger.LogError("ItemGroupId {ItemGroupId} does not exist or does not belong to UserId: {UserId}.", item.ItemGroupId, userId);
                    TempData["ErrorMessage"] = "Invalid item group selected.";
                    ViewBag.ItemGroupList = SetItemGroupDropDown();
                    return View("AddEditItem", item);
                }

                if (item.Id == null || item.Id == 0)
                {
                    _context.Items.Add(item);
                    _logger.LogInformation("New item added for UserId: {UserId}, Name: {Name}", userId, item.Name);
                    TempData["SuccessMessage"] = "Item successfully added!";
                }
                else
                {
                    var existingItem = await _context.Items
                        .FirstOrDefaultAsync(i => i.Id == item.Id && i.UserId == userId);
                    if (existingItem == null)
                    {
                        _logger.LogWarning("Item not found for Id: {Id} and UserId: {UserId}", item.Id, userId);
                        TempData["ErrorMessage"] = "Item not found or you do not have access to it.";
                        return RedirectToAction("ItemList");
                    }

                    existingItem.Name = item.Name;
                    existingItem.ItemGroupId = item.ItemGroupId;
                    _context.Items.Update(existingItem);
                    _logger.LogInformation("Item updated for Id: {Id}, UserId: {UserId}", item.Id, userId);
                    TempData["SuccessMessage"] = "Item successfully updated!";
                }

                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for Item Id: {Id}", item.Id);
                    TempData["ErrorMessage"] = "No changes were saved to the database.";
                }
                return RedirectToAction("ItemList");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547) // Foreign key violation
                {
                    _logger.LogError(ex, "Foreign key violation while saving Item for UserId: {UserId}", userId);
                    TempData["ErrorMessage"] = "Cannot save item due to an invalid item group reference.";
                }
                else if (sqlEx.Number == 2601 || sqlEx.Number == 2627) // Unique constraint violation
                {
                    _logger.LogError(ex, "Unique constraint violation while saving Item for UserId: {UserId}", userId);
                    TempData["ErrorMessage"] = "An item with this name already exists.";
                }
                else
                {
                    _logger.LogError(ex, "Database error while saving Item for UserId: {UserId}", userId);
                    TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                }
                ViewBag.ItemGroupList = SetItemGroupDropDown();
                return View("AddEditItem", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item for UserId: {UserId}", userId);
                TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                ViewBag.ItemGroupList = SetItemGroupDropDown();
                return View("AddEditItem", item);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int Id)
        {
            var userId = GetCurrentUserId();

            try
            {
                var item = await _context.Items
                    .FirstOrDefaultAsync(i => i.Id == Id && i.UserId == userId);
                if (item == null)
                {
                    _logger.LogWarning("DeleteItem - Item not found for Id: {Id} and UserId: {UserId}", Id, userId);
                    return Json(new { success = false, message = "Item not found or you do not have access to it." });
                }

                // Check for dependent records (e.g., in Transactions)
                var hasTransactions = await _context.Transactions.AnyAsync(t => t.ItemId == Id);
                if (hasTransactions)
                {
                    _logger.LogWarning("DeleteItem - Cannot delete item with associated transactions. Id: {Id}", Id);
                    return Json(new { success = false, message = "Cannot delete this item because it is referenced in transactions." });
                }

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Item deleted successfully for Id: {Id}, UserId: {UserId}", Id, userId);
                return Json(new { success = true, message = "Item successfully deleted!" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                _logger.LogError(ex, "Foreign key violation while deleting Item Id: {Id} for UserId: {UserId}", Id, userId);
                return Json(new { success = false, message = "Cannot delete this item because it is referenced in other records." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item for Id: {Id}, UserId: {UserId}", Id, userId);
                return Json(new { success = false, message = $"An error occurred while deleting the item: {ex.Message}" });
            }
        }
    }
}