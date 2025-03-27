using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

        private int GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        public async Task<IActionResult> AccountGroupList()
        {
            var userId = GetCurrentUserId();
            var groups = await _context.GroupAccounts
                .Where(g => g.UserId == userId) // Filter by UserId
                .ToListAsync();
            return View(groups);
        }

        public IActionResult AddEditAccountGroup(int Id)
        {
            var userId = GetCurrentUserId();

            if (Id <= 0)
            {
                return View(new AccountGroupModel { UserId = userId }); // Set UserId for new group
            }

            var group = _context.GroupAccounts
                .FirstOrDefault(g => g.Id == Id && g.UserId == userId); // Filter by UserId
            if (group == null)
            {
                return NotFound();
            }

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAddEditAccountGroup(AccountGroupModel accountGroup)
        {
            var userId = GetCurrentUserId();
            accountGroup.UserId = userId; // Ensure UserId is set

            if (!ModelState.IsValid)
            {
                return View("AddEditAccountGroup", accountGroup);
            }

            try
            {
                if (accountGroup.Id == null || accountGroup.Id == 0)
                {
                    _context.GroupAccounts.Add(accountGroup);
                    TempData["SuccessMessage"] = "Account group added successfully!";
                }
                else
                {
                    // Verify the group belongs to the current user
                    var existingGroup = await _context.GroupAccounts
                        .FirstOrDefaultAsync(g => g.Id == accountGroup.Id && g.UserId == userId);
                    if (existingGroup == null)
                    {
                        return NotFound();
                    }

                    existingGroup.GroupName = accountGroup.GroupName;
                    _context.GroupAccounts.Update(existingGroup);
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
            var userId = GetCurrentUserId();

            try
            {
                var group = await _context.GroupAccounts
                    .FirstOrDefaultAsync(g => g.Id == Id && g.UserId == userId); // Filter by UserId
                if (group == null)
                {
                    TempData["ErrorMessage"] = "Account group not found.";
                    return RedirectToAction("AccountGroupList");
                }

                _context.GroupAccounts.Remove(group);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Account group deleted successfully!";
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