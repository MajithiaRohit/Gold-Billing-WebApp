using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using System.Data;
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

        public async Task<IActionResult> ItemGroupList()
        {
            var itemGroups = await _context.ItemGroups.ToListAsync();
            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Date", typeof(DateTime)); // Added Date column
            foreach (var group in itemGroups)
            {
                table.Rows.Add(group.Id, group.Name, group.Date);
            }
            return View(table);
        }

        public IActionResult AddEditItemGroup(int? Id)
        {
            ItemGroupModel itemGroupModel = new ItemGroupModel();
            if (Id.HasValue && Id > 0)
            {
                itemGroupModel = _context.ItemGroups.Find(Id) ?? throw new Exception("Item group not found.");
            }
            return View(itemGroupModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItemGroup(ItemGroupModel itemGroup)
        {
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
                    _context.ItemGroups.Update(itemGroup);
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
        public async Task<IActionResult> DeleteItemGroup(int Id)
        {
            try
            {
                var itemGroup = await _context.ItemGroups.FindAsync(Id);
                if (itemGroup == null)
                {
                    TempData["ErrorMessage"] = "Item group not found.";
                    return RedirectToAction("ItemGroupList");
                }
                _context.ItemGroups.Remove(itemGroup);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Item group deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Deletion failed: {ex.Message}";
            }
            return RedirectToAction("ItemGroupList");
        }
    }
}