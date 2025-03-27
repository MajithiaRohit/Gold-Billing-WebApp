using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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

        private int GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        private List<AccountGroupDropDownModel> SetGroupDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.GroupAccounts
                .Where(g => g.UserId == userId) // Filter by UserId
                .Select(g => new AccountGroupDropDownModel
                {
                    Id = (int)g.Id!,
                    GroupName = g.GroupName
                })
                .ToList();
        }

        public async Task<IActionResult> AccountList()
        {
            var userId = GetCurrentUserId();
            var accounts = await _context.Accounts
                .Include(a => a.GroupAccount)
                .Where(a => a.UserId == userId) // Filter by UserId
                .ToListAsync();
            return View(accounts);
        }

        public IActionResult AddEditAccount(int AccountId)
        {
            var userId = GetCurrentUserId();
            ViewBag.groupList = SetGroupDropDown();

            if (AccountId <= 0)
            {
                return View(new AccountModel { UserId = userId }); // Set UserId for new account
            }

            var account = _context.Accounts
                .Include(a => a.GroupAccount)
                .FirstOrDefault(a => a.AccountId == AccountId && a.UserId == userId); // Filter by UserId
            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }

        public async Task<IActionResult> DeleteAccount(int AccountId)
        {
            var userId = GetCurrentUserId();

            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == AccountId && a.UserId == userId); // Filter by UserId
                if (account == null)
                {
                    TempData["ErrorMessage"] = "Account not found or you do not have access to it.";
                    return RedirectToAction("AccountList");
                }

                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Account deleted successfully!";
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
            var userId = GetCurrentUserId();
            account.UserId = userId; // Ensure UserId is set

            if (!ModelState.IsValid)
            {
                ViewBag.groupList = SetGroupDropDown();
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
                    // Verify the account belongs to the current user
                    var existingAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.AccountId == account.AccountId && a.UserId == userId);
                    if (existingAccount == null)
                    {
                        TempData["ErrorMessage"] = "Account not found or you do not have access to it.";
                        return RedirectToAction("AccountList");
                    }

                    existingAccount.AccountName = account.AccountName;
                    existingAccount.AccountGroupId = account.AccountGroupId;
                    existingAccount.Address = account.Address;
                    existingAccount.City = account.City;
                    existingAccount.Pincode = account.Pincode;
                    existingAccount.MobileNo = account.MobileNo;
                    existingAccount.PhoneNo = account.PhoneNo;
                    existingAccount.Email = account.Email;
                    existingAccount.Fine = account.Fine;
                    existingAccount.Amount = account.Amount;
                    existingAccount.Date = account.Date;
                    _context.Accounts.Update(existingAccount);
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