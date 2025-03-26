using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemController : Controller
    {
        private readonly AppDbContext _context;

        public ItemController(AppDbContext context)
        {
            _context = context;
        }

        public List<ItemGroupDropDownModel> SetItemGroupDropDown()
        {
            return _context.ItemGroups
                .Select(g => new ItemGroupDropDownModel
                {
                    Id = (int)g.Id!,
                    GroupName = g.Name
                })
                .ToList();
        }

        public async Task<IActionResult> ItemList()
        {
            var items = await _context.Items.Include(i => i.ItemGroup).ToListAsync();
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
            ViewBag.ItemGroupList = SetItemGroupDropDown();
            ItemModel item = new ItemModel();
            if (Id.HasValue && Id > 0)
            {
                item = _context.Items.Find(Id) ?? new ItemModel();
            }
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> SaveItem(ItemModel item)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemGroupList = SetItemGroupDropDown();
                return View("AddEditItem", item);
            }

            try
            {
                if (item.Id == null)
                {
                    _context.Items.Add(item);
                    TempData["SuccessMessage"] = "Item successfully added!";
                }
                else
                {
                    _context.Items.Update(item);
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
        public async Task<IActionResult> DeleteItem(int Id)
        {
            try
            {
                var item = await _context.Items.FindAsync(Id);
                if (item != null)
                {
                    _context.Items.Remove(item);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Item successfully deleted!" });
                }
                return Json(new { success = false, message = "Item not found." });
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