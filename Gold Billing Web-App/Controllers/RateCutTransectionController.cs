using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace Gold_Billing_Web_App.Controllers
{
    public class RateCutTransactionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RateCutTransactionController> _logger;

        public RateCutTransactionController(AppDbContext context, ILogger<RateCutTransactionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                _logger.LogWarning("User is not authenticated or UserId is invalid.");
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        private List<AccountDropDownModel> SetAccountDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.Accounts
                .Where(a => a.UserId == userId && (a.GroupAccount.GroupName == "Customer" || a.GroupAccount.GroupName == "Supplier"))
                .Include(a => a.GroupAccount)
                .Select(a => new AccountDropDownModel
                {
                    Id = a.AccountId,
                    AccountName = a.AccountName,
                    GroupName = a.GroupAccount != null ? a.GroupAccount.GroupName : ""
                })
                .ToList();
        }

        private string GenerateSequentialBillNo(string type)
        {
            try
            {
                var userId = GetCurrentUserId();
                string prefix = type == "GoldPurchaseRate" ? "GPR" : "GSR";
                var lastBill = _context.RateCutTransactions
                    .Where(t => t.BillNo.StartsWith(prefix) && t.UserId == userId)
                    .OrderByDescending(t => t.BillNo)
                    .Select(t => t.BillNo)
                    .FirstOrDefault();

                int lastNumber = 0;
                if (!string.IsNullOrEmpty(lastBill))
                {
                    string numberPart = lastBill.Substring(prefix.Length);
                    if (!int.TryParse(numberPart, out lastNumber))
                    {
                        _logger.LogWarning("Failed to parse bill number: {BillNo}", lastBill);
                        lastNumber = 0;
                    }
                }
                return $"{prefix}{(lastNumber + 1):D4}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sequential bill number for type {Type}", type);
                throw;
            }
        }

        [HttpGet]
        [Authorize] // Add if authentication is required
        public IActionResult GetNextBillNo(string? type)
        {
            try
            {
                if (string.IsNullOrEmpty(type) || !new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
                {
                    _logger.LogWarning("Invalid transaction type: {Type}", type);
                    return Json(new { success = false, error = "Invalid transaction type" });
                }

                string billNo = GenerateSequentialBillNo(type);
                return Json(new { success = true, billNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNextBillNo for type {Type}", type);
                return Json(new { success = false, error = $"Server error: {ex.Message}" });
            }
        }
        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var account = _context.Accounts
                    .Where(a => a.AccountId == accountId && a.UserId == userId)
                    .Select(a => new { a.Fine, a.Amount, a.LastUpdated })
                    .FirstOrDefault();

                return Json(account != null
                    ? new
                    {
                        fine = account.Fine,
                        amount = account.Amount,
                        lastBalanceDate = account.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                    : new { fine = 0.0m, amount = 0.0m, lastBalanceDate = (string?)null });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching previous balance for accountId {AccountId}", accountId);
                return Json(new { fine = 0.0m, amount = 0.0m, lastBalanceDate = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GenerateRateCutVoucher(string type = "GoldPurchaseRate", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
                _logger.LogWarning("Invalid transaction type: {Type}", type);
                return NotFound();
            }

            var userId = GetCurrentUserId();
            ViewBag.AccountDropDown = SetAccountDropDown();
            var model = new RateCutTransactionModel
            {
                UserId = userId,
                Type = type,
                Date = DateTime.Now,
                Weight = 0,
                Tunch = 0,
                Rate = 0,
                Amount = 0
            };

            if (!string.IsNullOrEmpty(billNo))
            {
                var transaction = _context.RateCutTransactions
                    .Where(t => t.UserId == userId && t.BillNo == billNo)
                    .Include(t => t.Account)
                    .FirstOrDefault();

                if (transaction == null)
                {
                    _logger.LogWarning("Transaction not found for BillNo: {BillNo} and UserId: {UserId}", billNo, userId);
                    return NotFound();
                }

                model = transaction;
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                if (accountId.HasValue && _context.Accounts.Any(a => a.AccountId == accountId.Value && a.UserId == userId))
                {
                    model.AccountId = accountId.Value;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRateCutTransaction([FromForm] RateCutTransactionModel model)
        {
            var userId = GetCurrentUserId();
            model.UserId = userId;
            ViewBag.AccountDropDown = SetAccountDropDown();

            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(kvp => kvp.Value.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid. Errors: {Errors}", errors);
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                var account = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == model.AccountId && a.UserId == userId);
                if (account == null)
                {
                    return Json(new { success = false, error = "Selected account does not exist." });
                }

                decimal fineGold = (model.Weight * model.Tunch) / 100;
                model.Amount = fineGold * model.Rate;

                var existingTransaction = await _context.RateCutTransactions
                    .FirstOrDefaultAsync(t => t.BillNo == model.BillNo && t.UserId == userId);

                decimal newFineAdjustment = fineGold;
                decimal newAmountAdjustment = model.Amount;
                string groupName = account.GroupAccount?.GroupName ?? "";

                if (groupName == "Supplier" || groupName == "Customer")
                {
                    if (model.Type == "GoldPurchaseRate")
                    {
                        newFineAdjustment = -fineGold;
                        newAmountAdjustment = model.Amount;
                    }
                    else if (model.Type == "GoldSaleRate")
                    {
                        newFineAdjustment = fineGold;
                        newAmountAdjustment = -model.Amount;
                    }
                }
                else
                {
                    return Json(new { success = false, error = "Account group must be Supplier or Customer." });
                }

                if (existingTransaction != null)
                {
                    existingTransaction.Date = model.Date;
                    existingTransaction.AccountId = model.AccountId;
                    existingTransaction.Type = model.Type;
                    existingTransaction.Weight = model.Weight;
                    existingTransaction.Tunch = model.Tunch;
                    existingTransaction.Rate = model.Rate;
                    existingTransaction.Amount = model.Amount;
                    existingTransaction.Narration = model.Narration;
                    _context.RateCutTransactions.Update(existingTransaction);
                }
                else
                {
                    _context.RateCutTransactions.Add(model);
                }

                account.Fine += newFineAdjustment;
                account.Amount += newAmountAdjustment;
                account.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction saved successfully for BillNo: {BillNo}", model.BillNo);

                string redirectUrl = Url.Action("GenerateRateCutVoucher", "RateCutTransaction")!;
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transaction for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = $"An error occurred: {ex.Message}" });
            }
        }
    }
}