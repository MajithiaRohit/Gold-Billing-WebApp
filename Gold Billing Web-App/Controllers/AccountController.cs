using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gold_Billing_Web_App.Controllers
{
    [LoginCheckAccess]
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AppDbContext context, ILogger<AccountController> logger)
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

        private List<AccountGroupDropDownModel> SetGroupDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.GroupAccounts
                .Where(g => g.UserId == userId)
                .Select(g => new AccountGroupDropDownModel
                {
                    Id = g.Id,
                    GroupName = g.GroupName
                })
                .ToList();
        }

        public async Task<IActionResult> AccountList()
        {
            var userId = GetCurrentUserId();
            var accounts = await _context.Accounts
                .Include(a => a.GroupAccount)
                .Where(a => a.UserId == userId)
                .ToListAsync();
            return View(accounts);
        }

        public IActionResult AddEditAccount(int AccountId)
        {
            var userId = GetCurrentUserId();
            ViewBag.groupList = SetGroupDropDown();

            if (AccountId <= 0)
            {
                _logger.LogInformation("Creating new account for UserId: {UserId}", userId);
                return View(new AccountModel { UserId = userId });
            }

            var account = _context.Accounts
                .Include(a => a.GroupAccount)
                .FirstOrDefault(a => a.AccountId == AccountId && a.UserId == userId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for AccountId: {AccountId} and UserId: {UserId}", AccountId, userId);
                return NotFound();
            }

            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAddEditAccount(AccountModel account)
        {
            var userId = GetCurrentUserId();
            account.UserId = userId;

            // Clear validation errors for navigation properties
            if (ModelState.ContainsKey("GroupAccount"))
            {
                ModelState.Remove("GroupAccount");
            }
            if (ModelState.ContainsKey("User"))
            {
                ModelState.Remove("User");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.groupList = SetGroupDropDown();
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ModelState invalid for Account. Errors: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join(", ", errors);
                return View("AddEditAccount", account);
            }

            try
            {
                // Verify that the UserId exists in the Users table
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogError("UserId {UserId} does not exist in the Users table.", userId);
                    TempData["ErrorMessage"] = "Invalid user. Please log in again.";
                    ViewBag.groupList = SetGroupDropDown();
                    return View("AddEditAccount", account);
                }

                // Verify that the AccountGroupId exists and belongs to the current user
                var groupExists = await _context.GroupAccounts.AnyAsync(g => g.Id == account.AccountGroupId && g.UserId == userId);
                if (!groupExists)
                {
                    _logger.LogError("AccountGroupId {AccountGroupId} does not exist or does not belong to UserId: {UserId}.", account.AccountGroupId, userId);
                    TempData["ErrorMessage"] = "Invalid account group selected.";
                    ViewBag.groupList = SetGroupDropDown();
                    return View("AddEditAccount", account);
                }

                if (account.AccountId == 0) // New account
                {
                    account.OpeningDate = DateTime.Now;
                    account.LastUpdated = DateTime.Now;
                    _context.Accounts.Add(account);
                    _logger.LogInformation("New account added for UserId: {UserId}, AccountName: {AccountName}", userId, account.AccountName);
                    TempData["SuccessMessage"] = "Account added successfully!";
                }
                else
                {
                    var existingAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.AccountId == account.AccountId && a.UserId == userId);
                    if (existingAccount == null)
                    {
                        _logger.LogWarning("Account not found for AccountId: {AccountId} and UserId: {UserId}", account.AccountId, userId);
                        TempData["ErrorMessage"] = "Account not found or you do not have access to it.";
                        return RedirectToAction("AccountList");
                    }

                    if (existingAccount.Fine != account.Fine || existingAccount.Amount != account.Amount)
                    {
                        existingAccount.LastUpdated = DateTime.Now;
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
                    _context.Accounts.Update(existingAccount);
                    _logger.LogInformation("Account updated for AccountId: {AccountId}, UserId: {UserId}", account.AccountId, userId);
                    TempData["SuccessMessage"] = "Account updated successfully!";
                }

                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for AccountId: {AccountId}", account.AccountId);
                    TempData["ErrorMessage"] = "No changes were saved to the database.";
                }
                return RedirectToAction("AccountList");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving account for AccountId: {AccountId}, UserId: {UserId}", account.AccountId, userId);
                ViewBag.groupList = SetGroupDropDown();
                TempData["ErrorMessage"] = $"Save failed: {ex.Message}";
                return View("AddEditAccount", account);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(int accountId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);

                if (account == null)
                {
                    _logger.LogWarning("DeleteAccount - Account not found for AccountId: {AccountId} and UserId: {UserId}", accountId, userId);
                    return Json(new { success = false, error = "Account not found or you do not have access to it." });
                }

                var hasTransactions = await _context.RateCutTransactions
                    .AnyAsync(t => t.AccountId == accountId);

                if (hasTransactions)
                {
                    _logger.LogWarning("DeleteAccount - Cannot delete account with transactions. AccountId: {AccountId}", accountId);
                    return Json(new { success = false, error = "You have already entered data; you cannot delete this account." });
                }

                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Account deleted successfully for AccountId: {AccountId}, UserId: {UserId}", accountId, userId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account for AccountId: {AccountId}", accountId);
                return Json(new { success = false, error = $"Error deleting account: {ex.Message}" });
            }
        }
    }
}