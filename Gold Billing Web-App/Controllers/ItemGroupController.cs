using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemGroupController : Controller
    {
        private readonly AppDbContext _context;

        public ItemGroupController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        public async Task<IActionResult> ItemGroupList()
        {
            var userId = GetCurrentUserId();
            var itemGroups = await _context.ItemGroups
                .Where(ig => ig.UserId == userId) // Filter by UserId
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
            ItemGroupModel itemGroupModel = new ItemGroupModel { UserId = userId }; // Set UserId for new group

            if (Id.HasValue && Id > 0)
            {
                itemGroupModel = _context.ItemGroups
                    .FirstOrDefault(ig => ig.Id == Id && ig.UserId == userId) // Filter by UserId
                    ?? throw new Exception("Item group not found or you do not have access to it.");
            }
            return View(itemGroupModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItemGroup(ItemGroupModel itemGroup)
        {
            var userId = GetCurrentUserId();
            itemGroup.UserId = userId; // Ensure UserId is set

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                ModelState.AddModelError("", "Validation failed: " + string.Join(", ", errors));
                return View("AddEditItemGroup", itemGroup);
            }

            try
            {
                itemGroup.Date = DateTime.Now; // Set Date on save
                if (!itemGroup.Id.HasValue || itemGroup.Id == 0) // Insert
                {
                    _context.ItemGroups.Add(itemGroup);
                    TempData["SuccessMessage"] = "Item group added successfully!";
                }
                else // Update
                {
                    // Verify the item group belongs to the current user
                    var existingGroup = await _context.ItemGroups
                        .FirstOrDefaultAsync(ig => ig.Id == itemGroup.Id && ig.UserId == userId);
                    if (existingGroup == null)
                    {
                        TempData["ErrorMessage"] = "Item group not found or you do not have access to it.";
                        return RedirectToAction("ItemGroupList");
                    }

                    existingGroup.Name = itemGroup.Name;
                    existingGroup.Date = itemGroup.Date;
                    _context.ItemGroups.Update(existingGroup);
                    TempData["SuccessMessage"] = "Item group updated successfully!";
                }
                await _context.SaveChangesAsync();
                return RedirectToAction("ItemGroupList");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                return View("AddEditItemGroup", itemGroup);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItemGroup(int id)
        {
            try
            {
                var itemGroup = await _context.ItemGroups
                    .FirstOrDefaultAsync(ig => ig.Id == id);

                if (itemGroup == null)
                {
                    return Json(new { success = false, error = "Item group not found" });
                }

                // Check for dependent items
                var hasItems = await _context.Items
                    .AnyAsync(i => i.ItemGroupId == id);

                if (hasItems)
                {
                    return Json(new { success = false, error = "This item group has associated items and cannot be deleted." });
                }

                _context.ItemGroups.Remove(itemGroup);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"Error deleting item group: {ex.Message}" });
            }
        }
    }
}