using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gold_Billing_Web_App.Controllers
{
    [LoginCheckAccess]
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        private List<AccountGroupDropDownModel> SetGroupDropDown()
        {
            return _context.GroupAccounts
                .Select(g => new AccountGroupDropDownModel
                {
                    Id = (int)g.Id!,
                    GroupName = g.GroupName
                })
                .ToList();
        }

        public async Task<IActionResult> AccountList()
        {
            var accounts = await _context.Accounts.Include(a => a.GroupAccount).ToListAsync();
            return View(accounts);
        }

        public IActionResult AddEditAccount(int AccountId)
        {
            ViewBag.groupList = SetGroupDropDown();

            if (AccountId <= 0) return View(new AccountModel());

            var account = _context.Accounts.Find(AccountId);
            if (account == null) return NotFound();

            return View(account);
        }

        public async Task<IActionResult> DeleteAccount(int AccountId)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(AccountId);
                if (account != null)
                {
                    _context.Accounts.Remove(account);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Account deleted successfully!";
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                TempData["ErrorMessage"] = "Cannot delete this account because it is referenced by other records.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Deletion failed: {ex.Message}";
            }
            return RedirectToAction("AccountList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAddEditAccount(AccountModel account)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.groupList = SetGroupDropDown();
                // Log invalid ModelState errors for debugging
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["ErrorMessage"] = "Validation failed: " + string.Join(", ", errors);
                return View("AddEditAccount", account);
            }

            try
            {
                account.Date = DateTime.Now;
                if (account.AccountId == null || account.AccountId == 0) // Handle both null and 0
                {
                    _context.Accounts.Add(account);
                    TempData["SuccessMessage"] = "Account added successfully!";
                }
                else
                {
                    _context.Accounts.Update(account);
                    TempData["SuccessMessage"] = "Account updated successfully!";
                }
                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    TempData["ErrorMessage"] = "No changes were saved to the database.";
                }
                return RedirectToAction("AccountList");
            }
            catch (Exception ex)
            {
                ViewBag.groupList = SetGroupDropDown();
                ModelState.AddModelError("", $"Save failed: {ex.Message}");
                TempData["ErrorMessage"] = $"Save failed: {ex.Message}";
                return View("AddEditAccount", account);
            }
        }
    }
}