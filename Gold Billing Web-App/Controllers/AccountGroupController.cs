using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gold_Billing_Web_App.Controllers
{
    [LoginCheckAccess]
    public class AccountGroupController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccountGroupController> _logger;

        public AccountGroupController(AppDbContext context, ILogger<AccountGroupController> logger)
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

        public async Task<IActionResult> AccountGroupList()
        {
            var userId = GetCurrentUserId();
            var groups = await _context.GroupAccounts
                .Where(g => g.UserId == userId)
                .ToListAsync();
            return View(groups);
        }

        public IActionResult AddEditAccountGroup(int Id)
        {
            var userId = GetCurrentUserId();

            if (Id == 0)
            {
                _logger.LogInformation("Creating new account group for UserId: {UserId}", userId);
                return View(new AccountGroupModel { UserId = userId });
            }

            var group = _context.GroupAccounts
                .FirstOrDefault(g => g.Id == Id && g.UserId == userId);
            if (group == null)
            {
                _logger.LogWarning("Account group not found for Id: {Id} and UserId: {UserId}", Id, userId);
                return NotFound();
            }

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAddEditAccountGroup(AccountGroupModel accountGroup)
        {
            var userId = GetCurrentUserId();
            accountGroup.UserId = userId;

            // Clear validation errors for navigation properties
            if (ModelState.ContainsKey("User"))
            {
                ModelState.Remove("User");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ModelState invalid for AccountGroup. Errors: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join(", ", errors);
                return View("AddEditAccountGroup", accountGroup);
            }

            try
            {
                // Verify that the UserId exists in the Users table
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogError("UserId {UserId} does not exist in the Users table.", userId);
                    TempData["ErrorMessage"] = "Invalid user. Please log in again.";
                    return View("AddEditAccountGroup", accountGroup);
                }

                if (accountGroup.Id == 0)
                {
                    _context.GroupAccounts.Add(accountGroup);
                    _logger.LogInformation("New account group added for UserId: {UserId}, GroupName: {GroupName}", userId, accountGroup.GroupName);
                    TempData["SuccessMessage"] = "Account group added successfully!";
                }
                else
                {
                    var existingGroup = await _context.GroupAccounts
                        .FirstOrDefaultAsync(g => g.Id == accountGroup.Id && g.UserId == userId);
                    if (existingGroup == null)
                    {
                        _logger.LogWarning("Account group not found for Id: {Id} and UserId: {UserId}", accountGroup.Id, userId);
                        return NotFound();
                    }

                    existingGroup.GroupName = accountGroup.GroupName;
                    _context.GroupAccounts.Update(existingGroup);
                    _logger.LogInformation("Account group updated for Id: {Id}, UserId: {UserId}", accountGroup.Id, userId);
                    TempData["SuccessMessage"] = "Account group updated successfully!";
                }

                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for AccountGroup Id: {Id}", accountGroup.Id);
                    TempData["ErrorMessage"] = "No changes were saved to the database.";
                }
                return RedirectToAction("AccountGroupList");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547) // Foreign key violation
                {
                    _logger.LogError(ex, "Foreign key violation while saving AccountGroup for UserId: {UserId}", userId);
                    TempData["ErrorMessage"] = "Cannot save account group due to an invalid user reference.";
                }
                else if (sqlEx.Number == 2601 || sqlEx.Number == 2627) // Unique constraint violation
                {
                    _logger.LogError(ex, "Unique constraint violation while saving AccountGroup for UserId: {UserId}", userId);
                    TempData["ErrorMessage"] = "An account group with this name already exists.";
                }
                else
                {
                    _logger.LogError(ex, "Database error while saving AccountGroup for UserId: {UserId}", userId);
                    TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                }
                return View("AddEditAccountGroup", accountGroup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving AccountGroup for UserId: {UserId}", userId);
                TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                return View("AddEditAccountGroup", accountGroup);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccountGroup(int Id)
        {
            var userId = GetCurrentUserId();

            try
            {
                var group = await _context.GroupAccounts
                    .FirstOrDefaultAsync(g => g.Id == Id && g.UserId == userId);
                if (group == null)
                {
                    _logger.LogWarning("Account group not found for Id: {Id} and UserId: {UserId}", Id, userId);
                    TempData["ErrorMessage"] = "Account group not found.";
                    return RedirectToAction("AccountGroupList");
                }

                _context.GroupAccounts.Remove(group);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Account group deleted successfully for Id: {Id}, UserId: {UserId}", Id, userId);
                TempData["SuccessMessage"] = "Account group deleted successfully!";
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                _logger.LogError(ex, "Foreign key violation while deleting AccountGroup Id: {Id} for UserId: {UserId}", Id, userId);
                TempData["ErrorMessage"] = "Cannot delete this account group because it is referenced by other records.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AccountGroup Id: {Id} for UserId: {UserId}", Id, userId);
                TempData["ErrorMessage"] = $"Deletion failed: {ex.Message}";
            }
            return RedirectToAction("AccountGroupList");
        }
    }
}