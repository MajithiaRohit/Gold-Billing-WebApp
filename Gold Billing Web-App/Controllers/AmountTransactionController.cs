using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System.Security.Claims;

namespace Gold_Billing_Web_App.Controllers
{
    public class AmountTransactionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AmountTransactionController> _logger;

        public AmountTransactionController(AppDbContext context, ILogger<AmountTransactionController> logger)
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

        private List<PaymentModeDropDownModel> SetPaymentModeDropDown()
        {
            return _context.PaymentModes
                .Select(pm => new PaymentModeDropDownModel
                {
                    Id = pm.Id,
                    ModeName = pm.ModeName
                })
                .ToList();
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string type)
        {
            if (!new[] { "Payment", "Receive" }.Contains(type))
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

        private string GenerateSequentialBillNo(string transactionType)
        {
            var userId = GetCurrentUserId();
            string prefix = transactionType == "Payment" ? "PAYM" : "RECV";
            var lastBill = _context.AmountTransactions
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
        public IActionResult GetPreviousBalance(int accountId)
        {
            var userId = GetCurrentUserId();
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId && a.UserId == userId)
                .Select(a => new { a.Fine, a.Amount })
                .FirstOrDefault();

            if (account == null)
            {
                _logger.LogWarning("Account not found for AccountId: {AccountId} and UserId: {UserId}", accountId, userId);
                return Json(new { fine = 0.0m, amount = 0.0m });
            }

            return Json(new { fine = account.Fine, amount = account.Amount });
        }

        public IActionResult GenerateAmountTransactionVoucher(string type = "Payment", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Payment", "Receive" }.Contains(type))
            {
                _logger.LogWarning("Invalid transaction type: {Type}", type);
                return NotFound();
            }

            var userId = GetCurrentUserId();
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.PaymentModeDropDown = SetPaymentModeDropDown();
            var model = new AmountTransactionModel { UserId = userId };

            bool isExistingTransaction = !string.IsNullOrEmpty(billNo) && _context.AmountTransactions.Any(t => t.BillNo == billNo && t.UserId == userId);
            ViewBag.IsExistingTransaction = isExistingTransaction;

            if (isExistingTransaction)
            {
                var transaction = _context.AmountTransactions
                    .Where(t => t.BillNo == billNo && t.UserId == userId)
                    .Include(t => t.Account)
                    .Include(t => t.PaymentMode)
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
                model.Date = DateTime.Now;
                model.Type = type;
                model.Amount = 0;
                model.PaymentModeId = 0;
                if (accountId.HasValue)
                {
                    model.AccountId = accountId.Value;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAmountTransaction([FromForm] AmountTransactionModel model)
        {
            var userId = GetCurrentUserId();
            model.UserId = userId;
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.PaymentModeDropDown = SetPaymentModeDropDown();

            _logger.LogInformation("Received model: {@Model}", new
            {
                model.Id,
                model.BillNo,
                model.Date,
                model.AccountId,
                model.Type,
                model.PaymentModeId,
                model.Amount,
                model.Narration,
                model.UserId
            });

            model.Type = Request.Form["Type"].FirstOrDefault() ?? "Payment";

            if (ModelState.ContainsKey("User"))
            {
                ModelState.Remove("User");
            }

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
                var existingTransaction = await _context.AmountTransactions
                    .FirstOrDefaultAsync(t => t.BillNo == model.BillNo && t.UserId == userId);

                var selectedAccount = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == model.AccountId && a.UserId == userId);

                if (selectedAccount == null)
                {
                    _logger.LogWarning("Account not found for AccountId: {AccountId} and UserId: {UserId}", model.AccountId, userId);
                    return Json(new { success = false, error = "Selected account not found." });
                }

                var paymentMode = await _context.PaymentModes
                    .FirstOrDefaultAsync(pm => pm.Id == model.PaymentModeId);

                if (paymentMode == null)
                {
                    _logger.LogWarning("Payment mode not found for PaymentModeId: {PaymentModeId}", model.PaymentModeId);
                    return Json(new { success = false, error = "Payment mode not found." });
                }

                decimal selectedAccountAdjustment = 0;
                string selectedGroupName = selectedAccount.GroupAccount?.GroupName ?? "";
                if (selectedGroupName == "Supplier")
                {
                    selectedAccountAdjustment = model.Type == "Payment" ? -model.Amount : model.Amount;
                }
                else if (selectedGroupName == "Customer")
                {
                    selectedAccountAdjustment = model.Type == "Receive" ? -model.Amount : model.Amount;
                }

                var bankModes = new[] { "RTGS", "NEFT", "CHEQUE", "GPAY", "PHONEPE" };
                bool isBankMode = bankModes.Contains(paymentMode.ModeName, StringComparer.OrdinalIgnoreCase);
                bool isCashMode = paymentMode.ModeName.Equals("CASH", StringComparison.OrdinalIgnoreCase);

                AccountModel? paymentAccount = null;
                if (isBankMode)
                {
                    paymentAccount = await _context.Accounts
                        .Include(a => a.GroupAccount)
                        .FirstOrDefaultAsync(a => a.UserId == userId && a.GroupAccount.GroupName == "Bank");

                    if (paymentAccount == null)
                    {
                        var bankGroup = await _context.GroupAccounts
                            .FirstOrDefaultAsync(g => g.GroupName == "Bank" && g.UserId == userId);
                        if (bankGroup == null)
                        {
                            bankGroup = new AccountGroupModel { GroupName = "Bank", UserId = userId };
                            _context.GroupAccounts.Add(bankGroup);
                            await _context.SaveChangesAsync();
                        }

                        paymentAccount = new AccountModel
                        {
                            AccountName = "Default Bank Account",
                            AccountGroupId = bankGroup.Id,
                            UserId = userId,
                            OpeningDate = DateTime.Now,
                            Amount = 0,
                            Fine = 0,
                            LastUpdated = DateTime.Now,
                            Address = "N/A",
                            City = "N/A",
                            MobileNo = "N/A",
                            PhoneNo = "N/A"
                        };
                        _context.Accounts.Add(paymentAccount);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Created default bank account for UserId: {UserId}", userId);
                    }
                }
                else if (isCashMode)
                {
                    paymentAccount = await _context.Accounts
                        .Include(a => a.GroupAccount)
                        .FirstOrDefaultAsync(a => a.UserId == userId && a.GroupAccount.GroupName == "Cash");

                    if (paymentAccount == null)
                    {
                        var cashGroup = await _context.GroupAccounts
                            .FirstOrDefaultAsync(g => g.GroupName == "Cash" && g.UserId == userId);
                        if (cashGroup == null)
                        {
                            cashGroup = new AccountGroupModel { GroupName = "Cash", UserId = userId };
                            _context.GroupAccounts.Add(cashGroup);
                            await _context.SaveChangesAsync();
                        }

                        paymentAccount = new AccountModel
                        {
                            AccountName = "Cash Account",
                            AccountGroupId = cashGroup.Id,
                            UserId = userId,
                            OpeningDate = DateTime.Now,
                            Amount = 0,
                            Fine = 0,
                            LastUpdated = DateTime.Now,
                            Address = "N/A",
                            City = "N/A",
                            MobileNo = "N/A",
                            PhoneNo = "N/A"
                        };
                        _context.Accounts.Add(paymentAccount);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Created cash account for UserId: {UserId}", userId);
                    }
                }

                decimal paymentAccountAdjustment = 0;
                if (paymentAccount != null)
                {
                    paymentAccountAdjustment = model.Type == "Payment" ? model.Amount : -model.Amount;
                    paymentAccount.Amount += paymentAccountAdjustment;
                    paymentAccount.LastUpdated = DateTime.Now;
                }

                decimal previousSelectedAccountAdjustment = 0;
                if (existingTransaction != null)
                {
                    if (selectedGroupName == "Supplier")
                    {
                        previousSelectedAccountAdjustment = existingTransaction.Type == "Payment" ? existingTransaction.Amount : -existingTransaction.Amount;
                    }
                    else if (selectedGroupName == "Customer")
                    {
                        previousSelectedAccountAdjustment = existingTransaction.Type == "Receive" ? existingTransaction.Amount : -existingTransaction.Amount;
                    }

                    existingTransaction.Date = model.Date;
                    existingTransaction.AccountId = model.AccountId;
                    existingTransaction.Type = model.Type;
                    existingTransaction.PaymentModeId = model.PaymentModeId;
                    existingTransaction.Amount = model.Amount;
                    existingTransaction.Narration = model.Narration;
                    _context.AmountTransactions.Update(existingTransaction);
                }
                else
                {
                    _context.AmountTransactions.Add(model);
                }

                selectedAccount.Amount = selectedAccount.Amount - previousSelectedAccountAdjustment + selectedAccountAdjustment;
                selectedAccount.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction saved successfully for BillNo: {BillNo}", model.BillNo);
                if (paymentAccount != null)
                {
                    _logger.LogInformation("Updated payment account {AccountName} (Id: {AccountId}) with adjustment: {Adjustment}",
                        paymentAccount.AccountName, paymentAccount.AccountId, paymentAccountAdjustment);
                }

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