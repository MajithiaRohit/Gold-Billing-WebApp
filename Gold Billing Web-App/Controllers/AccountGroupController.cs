using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Gold_Billing_Web_App.Controllers
{
    [LoginCheckAccess]
    public class AccountGroupController : Controller
    {
        private readonly AppDbContext _context;

        public AccountGroupController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> AccountGroupList()
        {
            var groups = await _context.GroupAccounts.ToListAsync();
            return View(groups);
        }

        public IActionResult AddEditAccountGroup(int Id)
        {
            if (Id <= 0) return View(new AccountGroupModel());

            var group = _context.GroupAccounts.Find(Id);
            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAddEditAccountGroup(AccountGroupModel accountGroup)
        {
            if (!ModelState.IsValid)
            {
                return View("AddEditAccountGroup", accountGroup);
            }

            try
            {
                if (accountGroup.Id == null)
                {
                    _context.GroupAccounts.Add(accountGroup);
                    TempData["SuccessMessage"] = "Account group added successfully!";
                }
                else
                {
                    _context.GroupAccounts.Update(accountGroup);
                    TempData["SuccessMessage"] = "Account group updated successfully!";
                }
                await _context.SaveChangesAsync();
                return RedirectToAction("AccountGroupList");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                return View("AddEditAccountGroup", accountGroup);
            }
        }

        public async Task<IActionResult> DeleteAccountGroup(int Id)
        {
            try
            {
                var group = await _context.GroupAccounts.FindAsync(Id);
                if (group != null)
                {
                    _context.GroupAccounts.Remove(group);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Account group deleted successfully!";
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                TempData["ErrorMessage"] = "Cannot delete this account group because it is referenced by other records.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Deletion failed: {ex.Message}";
            }
            return RedirectToAction("AccountGroupList");
        }
    }
}