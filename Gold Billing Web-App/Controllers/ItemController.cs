using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemController : Controller
    {
        private readonly AppDbContext _context;

        public ItemController(AppDbContext context)
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

        public List<ItemGroupDropDownModel> SetItemGroupDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.ItemGroups
                .Where(g => g.UserId == userId) // Filter by UserId
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
                .Include(i => i.ItemGroup)
                .Where(i => i.UserId == userId) // Filter by UserId
                .ToListAsync();

            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("ItemGroupId", typeof(int));
            table.Columns.Add("GroupName", typeof(string));
            foreach (var item in items)
            {
                table.Rows.Add(item.Id, item.Name, item.ItemGroupId, item.ItemGroup?.Name);
            }
            return View(table);
        }

        public IActionResult AddEditItem(int? Id)
        {
            var userId = GetCurrentUserId();
            ViewBag.ItemGroupList = SetItemGroupDropDown();
            ItemModel item = new ItemModel { UserId = userId }; // Set UserId for new item

            if (Id.HasValue && Id > 0)
            {
                item = _context.Items
                    .Include(i => i.ItemGroup)
                    .FirstOrDefault(i => i.Id == Id && i.UserId == userId) // Filter by UserId
                    ?? throw new Exception("Item not found or you do not have access to it.");
            }
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItem(ItemModel item)
        {
            var userId = GetCurrentUserId();
            item.UserId = userId; // Ensure UserId is set

            if (!ModelState.IsValid)
            {
                ViewBag.ItemGroupList = SetItemGroupDropDown();
                return View("AddEditItem", item);
            }

            try
            {
                if (item.Id == null || item.Id == 0)
                {
                    _context.Items.Add(item);
                    TempData["SuccessMessage"] = "Item successfully added!";
                }
                else
                {
                    // Verify the item belongs to the current user
                    var existingItem = await _context.Items
                        .FirstOrDefaultAsync(i => i.Id == item.Id && i.UserId == userId);
                    if (existingItem == null)
                    {
                        TempData["ErrorMessage"] = "Item not found or you do not have access to it.";
                        return RedirectToAction("ItemList");
                    }

                    existingItem.Name = item.Name;
                    existingItem.ItemGroupId = item.ItemGroupId;
                    _context.Items.Update(existingItem);
                    TempData["SuccessMessage"] = "Item successfully updated!";
                }
                await _context.SaveChangesAsync();
                return RedirectToAction("ItemList");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while saving the item: " + ex.Message;
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
                    .FirstOrDefaultAsync(i => i.Id == Id && i.UserId == userId); // Filter by UserId
                if (item == null)
                {
                    return Json(new { success = false, message = "Item not found or you do not have access to it." });
                }

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Item successfully deleted!" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                return Json(new { success = false, message = "Cannot delete this item because it is referenced in other records." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the item: " + ex.Message });
            }
        }
    }
}