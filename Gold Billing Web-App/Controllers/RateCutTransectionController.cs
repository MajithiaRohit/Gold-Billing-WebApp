using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session; // Assuming this namespace for CommonVariable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for logging

namespace Gold_Billing_Web_App.Controllers
{
    public class RateCutTransectionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RateCutTransectionController> _logger; // Added for logging

        public RateCutTransectionController(AppDbContext context, ILogger<RateCutTransectionController> logger)
        {
            _context = context;
            _logger = logger;
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

        private List<AccountDropDownModel> SetAccountDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.Accounts
                .Where(a => a.UserId == userId) // Filter by UserId
                .Include(a => a.GroupAccount)
                .Select(a => new AccountDropDownModel
                {
                    Id = a.AccountId!.Value,
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
                .Where(t => t.BillNo.StartsWith(prefix) && t.UserId == userId) // Filter by UserId
                .OrderByDescending(t => t.BillNo)
                .Select(t => t.BillNo)
                .FirstOrDefault();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastBill))
            {
                lastNumber = int.Parse(lastBill.Substring(prefix.Length));
            }
            return $"{prefix}{(lastNumber + 1):D4}";
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string? type)
        {
            if (string.IsNullOrEmpty(type) || !new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
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
                .Where(a => a.AccountId == accountId && a.UserId == userId) // Filter by UserId
                .Select(a => new { a.Fine, a.Amount, a.Date })
                .FirstOrDefault();

            if (account != null)
            {
                return Json(new
                {
                    fine = account.Fine,
                    amount = account.Amount,
                    lastBalanceDate = account.Date.ToString("yyyy-MM-dd")
                });
            }
            return Json(new { fine = 0.0, amount = 0.0, lastBalanceDate = (string?)null });
        }

        public IActionResult GenrateRateCutVoucher(string type = "GoldPurchaseRate", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
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
                    return NotFound();
                }

                model = transaction;
                ViewBag.SelectedAccountId = model.AccountId; // Set only for existing transactions
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
                    Errors = kvp.Value!.Errors.Select(e => e.ErrorMessage).ToList()
                }).Where(x => x.Errors.Any()).ToList();
                _logger.LogWarning("ModelState invalid. Detailed errors: {@Errors}", errors);
                return Json(new { success = false, error = string.Join("; ", errors.SelectMany(e => e.Errors)) });
            }

            try
            {
                // Validate AccountId exists for the current user
                var accountExists = await _context.Accounts
                    .AnyAsync(a => a.AccountId == model.AccountId && a.UserId == userId);
                if (!accountExists)
                {
                    _logger.LogWarning("Invalid AccountId {AccountId} for UserId {UserId}", model.AccountId, userId);
                    return Json(new { success = false, error = "Selected account does not exist or is not associated with your user." });
                }

                decimal fineGold = (model.Weight * model.Tunch) / 100;
                model.Amount = fineGold * model.Rate;

                var existingTransaction = await _context.RateCutTransactions
                    .FirstOrDefaultAsync(t => t.BillNo == model.BillNo && t.UserId == userId);

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

                var account = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == model.AccountId && a.UserId == userId);

                if (account != null)
                {
                    decimal fineAdjustment = fineGold;
                    decimal amountAdjustment = model.Amount;

                    if (account.GroupAccount?.GroupName == "Supplier")
                    {
                        if (model.Type == "GoldPurchaseRate")
                        {
                            fineAdjustment = -fineGold; // Decrease Fine (paying metal)
                            amountAdjustment = model.Amount; // Increase Amount (receiving money)
                        }
                        else if (model.Type == "GoldSaleRate")
                        {
                            fineAdjustment = fineGold; // Increase Fine (receiving metal)
                            amountAdjustment = -model.Amount; // Decrease Amount (paying money)
                        }
                    }
                    else if (account.GroupAccount?.GroupName == "Customer")
                    {
                        if (model.Type == "GoldPurchaseRate")
                        {
                            fineAdjustment = -fineGold; // Decrease Fine (paying metal)
                            amountAdjustment = model.Amount; // Increase Amount (receiving money)
                        }
                        else if (model.Type == "GoldSaleRate")
                        {
                            fineAdjustment = fineGold; // Increase Fine (receiving metal)
                            amountAdjustment = -model.Amount; // Decrease Amount (paying money)
                        }
                    }

                    account.Fine += fineAdjustment;
                    account.Amount += amountAdjustment;
                }

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