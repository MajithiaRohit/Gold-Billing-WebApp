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
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
                .Where(a => a.UserId == userId)
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
                if (!int.TryParse(lastBill.Substring(prefix.Length), out lastNumber))
                {
                    _logger.LogWarning("Failed to parse bill number: {BillNo}", lastBill);
                    lastNumber = 0;
                }
            }
            return $"{prefix}{(lastNumber + 1):D4}";
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string? type)
        {
            if (string.IsNullOrEmpty(type) || !new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
                _logger.LogWarning("Invalid transaction type: {Type}", type);
                return BadRequest("Invalid transaction type");
            }

            try
            {
                string billNo = GenerateSequentialBillNo(type);
                return Json(new { success = true, billNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bill number for type {Type}", type);
                return Json(new { success = false, error = $"Error generating bill number: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            var userId = GetCurrentUserId();
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId && a.UserId == userId)
                .Select(a => new { a.Fine, a.Amount, a.LastUpdated })
                .FirstOrDefault();

            if (account != null)
            {
                return Json(new
                {
                    fine = account.Fine,
                    amount = account.Amount,
                    lastBalanceDate = account.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            return Json(new { fine = 0.0m, amount = 0.0m, lastBalanceDate = (string?)null });
        }

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
                    .Where(t => t.UserId == userId)
                    .Include(t => t.Account)
                    .FirstOrDefault(t => t.BillNo == billNo);

                if (transaction == null)
                {
                    _logger.LogWarning("Transaction not found for BillNo: {BillNo} and UserId: {UserId}", billNo, userId);
                    return NotFound();
                }

                model = transaction;
                ViewBag.SelectedAccountId = model.AccountId;
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                if (accountId.HasValue && _context.Accounts.Any(a => a.AccountId == accountId.Value && a.UserId == userId))
                {
                    model.AccountId = accountId.Value;
                    ViewBag.SelectedAccountId = accountId.Value;
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

            model.Type = Request.Form["Type"].FirstOrDefault() ?? "GoldPurchaseRate";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Select(kvp => new
                {
                    Key = kvp.Key,
                    Errors = kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
                }).Where(x => x.Errors.Any()).ToList();
                _logger.LogWarning("ModelState invalid. Detailed errors: {@Errors}", errors);
                return Json(new { success = false, error = string.Join("; ", errors.SelectMany(e => e.Errors)) });
            }

            try
            {
                // Validate AccountId exists for the current user
                var account = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == model.AccountId && a.UserId == userId);
                if (account == null)
                {
                    _logger.LogWarning("Invalid AccountId {AccountId} for UserId {UserId}", model.AccountId, userId);
                    return Json(new { success = false, error = "Selected account does not exist or is not associated with your user." });
                }

                // Calculate Fine Gold and Amount
                decimal fineGold = (model.Weight * model.Tunch) / 100;
                model.Amount = fineGold * model.Rate;

                var existingTransaction = await _context.RateCutTransactions
                    .FirstOrDefaultAsync(t => t.BillNo == model.BillNo && t.UserId == userId);

                // Calculate adjustments for the new transaction
                decimal newFineAdjustment = fineGold;
                decimal newAmountAdjustment = model.Amount;

                string groupName = account.GroupAccount?.GroupName ?? "";
                if (groupName == "Supplier")
                {
                    if (model.Type == "GoldPurchaseRate")
                    {
                        newFineAdjustment = -fineGold; // Decrease Fine (paying metal)
                        newAmountAdjustment = model.Amount; // Increase Amount (receiving money)
                    }
                    else if (model.Type == "GoldSaleRate")
                    {
                        newFineAdjustment = fineGold; // Increase Fine (receiving metal)
                        newAmountAdjustment = -model.Amount; // Decrease Amount (paying money)
                    }
                }
                else if (groupName == "Customer")
                {
                    if (model.Type == "GoldPurchaseRate")
                    {
                        newFineAdjustment = -fineGold; // Decrease Fine (paying metal)
                        newAmountAdjustment = model.Amount; // Increase Amount (receiving money)
                    }
                    else if (model.Type == "GoldSaleRate")
                    {
                        newFineAdjustment = fineGold; // Increase Fine (receiving metal)
                        newAmountAdjustment = -model.Amount; // Decrease Amount (paying money)
                    }
                }

                // If this is an update, reverse the previous transaction's effect
                decimal previousFineAdjustment = 0;
                decimal previousAmountAdjustment = 0;
                if (existingTransaction != null)
                {
                    decimal previousFineGold = (existingTransaction.Weight * existingTransaction.Tunch) / 100;
                    previousFineAdjustment = previousFineGold;
                    previousAmountAdjustment = existingTransaction.Amount;

                    if (groupName == "Supplier")
                    {
                        if (existingTransaction.Type == "GoldPurchaseRate")
                        {
                            previousFineAdjustment = -previousFineGold; // Reverse: Increase Fine
                            previousAmountAdjustment = existingTransaction.Amount; // Reverse: Decrease Amount
                        }
                        else if (existingTransaction.Type == "GoldSaleRate")
                        {
                            previousFineAdjustment = previousFineGold; // Reverse: Decrease Fine
                            previousAmountAdjustment = -existingTransaction.Amount; // Reverse: Increase Amount
                        }
                    }
                    else if (groupName == "Customer")
                    {
                        if (existingTransaction.Type == "GoldPurchaseRate")
                        {
                            previousFineAdjustment = -previousFineGold; // Reverse: Increase Fine
                            previousAmountAdjustment = existingTransaction.Amount; // Reverse: Decrease Amount
                        }
                        else if (existingTransaction.Type == "GoldSaleRate")
                        {
                            previousFineAdjustment = previousFineGold; // Reverse: Decrease Fine
                            previousAmountAdjustment = -existingTransaction.Amount; // Reverse: Increase Amount
                        }
                    }

                    // Update the existing transaction
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
                    // Add a new transaction
                    _context.RateCutTransactions.Add(model);
                }

                // Adjust the account balance
                account.Fine = account.Fine - previousFineAdjustment + newFineAdjustment;
                account.Amount = account.Amount - previousAmountAdjustment + newAmountAdjustment;

                // Update LastUpdated since Fine or Amount has changed
                account.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction saved successfully for BillNo: {BillNo}", model.BillNo);

                string redirectUrl = Url.Action("Index", "Home")!;
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transaction for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }
    }
}