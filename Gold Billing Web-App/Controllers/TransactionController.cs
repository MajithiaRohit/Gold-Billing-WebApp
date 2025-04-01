using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Models.ViewModels;
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
    public class TransactionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(AppDbContext context, ILogger<TransactionController> logger)
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
                .Select(a => new AccountDropDownModel
                {
                    Id = a.AccountId,
                    AccountName = a.AccountName
                })
                .ToList();
        }

        private string GenerateSequentialBillNo(string transactionType)
        {
            var userId = GetCurrentUserId();
            string prefix = transactionType switch
            {
                "Purchase" => "P",
                "Sale" => "S",
                "PurchaseReturn" => "PR",
                "SaleReturn" => "SR",
                _ => "P"
            };

            var lastBill = _context.Transactions
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

        private List<ItemDropDownModel> SetItemDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.Items
                .Include(i => i.ItemGroup)
                .Where(i => i.UserId == userId && i.ItemGroup!.Name != "Gold Metal")
                .Select(i => new ItemDropDownModel
                {
                    Id = i.Id!.Value,
                    ItemName = i.Name!,
                    GroupName = i.ItemGroup!.Name ?? ""
                })
                .ToList();
        }

        private void CalculateDerivedFields(TransactionModel model, string groupName)
        {
            if (groupName == "PC Gold Jewelry")
            {
                model.NetWt = 0;
                model.TW = 0;
                model.Fine = 0;
                model.Amount = (model.Pc ?? 0) * (model.Rate ?? 0);
            }
            else
            {
                model.NetWt = (model.Weight ?? 0) - (model.Less ?? 0);
                model.TW = (model.Tunch ?? 0) + (model.Wastage ?? 0);
                model.Fine = (model.NetWt ?? 0) * (model.TW ?? 0) / 100;
                model.Amount = (model.Fine ?? 0) * (model.Rate ?? 0);
            }
        }

        private bool CheckIfTransactionExists(string billNo)
        {
            var userId = GetCurrentUserId();
            return _context.Transactions.Any(t => t.BillNo == billNo && t.UserId == userId);
        }

        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            var userId = GetCurrentUserId();
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId && a.UserId == userId)
                .Select(a => new
                {
                    Fine = a.Fine,
                    Amount = a.Amount,
                    LastUpdated = a.LastUpdated
                })
                .FirstOrDefault();

            if (account != null)
            {
                return Json(new
                {
                    fine = account.Fine,
                    amount = account.Amount,
                    date = account.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            return Json(new { fine = 0.0m, amount = 0.0m, date = (string?)null });
        }

        [HttpGet]
        public IActionResult AddTransaction(string type, int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Purchase", "Sale", "PurchaseReturn", "SaleReturn" }.Contains(type))
            {
                _logger.LogWarning("Invalid transaction type: {Type}", type);
                return NotFound();
            }

            var userId = GetCurrentUserId();
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            var model = new TransactionViewModel { UserId = userId };

            bool isExistingTransaction = !string.IsNullOrEmpty(billNo) && CheckIfTransactionExists(billNo);
            ViewBag.IsExistingTransaction = isExistingTransaction;

            if (isExistingTransaction)
            {
                var transactions = _context.Transactions
                    .Where(t => t.BillNo == billNo && t.UserId == userId)
                    .ToList();

                if (!transactions.Any())
                {
                    _logger.LogWarning("Transaction not found for BillNo: {BillNo} and UserId: {UserId}", billNo, userId);
                    return NotFound();
                }

                model.BillNo = billNo!;
                model.Date = transactions.First().Date;
                model.Narration = transactions.First().Narration;
                model.TransactionType = type;
                model.Items = transactions;
                model.UserId = userId;

                if (model.Items.Any() && model.Items[0].AccountId.HasValue)
                {
                    ViewBag.SelectedAccountId = model.Items[0].AccountId;
                }
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                model.Date = DateTime.Now;
                model.TransactionType = type;
                model.Items = new List<TransactionModel> { new TransactionModel { TransactionType = type, UserId = userId } };
                model.UserId = userId;

                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.Items[0].AccountId = accountId;
                }
            }

            _logger.LogInformation("GET - BillNo set to: {BillNo}", model.BillNo);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTransaction(TransactionViewModel model, int? SelectedAccountId)
        {
            var userId = GetCurrentUserId();
            model.UserId = userId;

            _logger.LogInformation("POST - Received BillNo: {BillNo}, Date: {Date}", model.BillNo, model.Date);
            if (model.Items != null)
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    _logger.LogInformation("POST - Items[{Index}].BillNo: {BillNo}, Date: {Date}", i, model.Items[i].BillNo, model.Items[i].Date);
                }
            }

            // Log the raw form data
            var formData = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
            _logger.LogInformation("POST - Form Data: {@FormData}", formData);

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            // Clear validation errors for navigation properties
            if (ModelState.ContainsKey("Account"))
            {
                ModelState.Remove("Account");
            }
            if (ModelState.ContainsKey("Item"))
            {
                ModelState.Remove("Item");
            }
            if (model.Items != null)
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    if (ModelState.ContainsKey($"Items[{i}].Account"))
                    {
                        ModelState.Remove($"Items[{i}].Account");
                    }
                    if (ModelState.ContainsKey($"Items[{i}].Item"))
                    {
                        ModelState.Remove($"Items[{i}].Item");
                    }
                    if (ModelState.ContainsKey($"Items[{i}].User"))
                    {
                        ModelState.Remove($"Items[{i}].User");
                    }
                }
            }

            if (string.IsNullOrEmpty(model.TransactionType))
            {
                model.TransactionType = Request.Form["TransactionType"].FirstOrDefault() ?? "Purchase";
            }

            if (string.IsNullOrEmpty(model.BillNo))
            {
                model.BillNo = GenerateSequentialBillNo(model.TransactionType);
                _logger.LogInformation("POST - Generated new BillNo: {BillNo}", model.BillNo);
            }

            if (!SelectedAccountId.HasValue)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
            }
            else
            {
                foreach (var item in model.Items)
                {
                    if (!item.ItemId.HasValue)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Item is required.");
                        continue;
                    }

                    // Verify that the ItemId exists and belongs to the current user
                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Selected item does not exist or is not associated with your user.");
                        continue;
                    }

                    string groupName = itemGroups.ContainsKey(item.ItemId.Value) ? itemGroups[item.ItemId.Value] : "";
                    switch (groupName)
                    {
                        case "Gold Jewelry":
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            break;
                        case "PC Gold Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                        case "PC/Weight Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                        default:
                            ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Invalid item group.");
                            break;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("POST - Validation Errors: {Errors}", string.Join("; ", errors));
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == SelectedAccountId && a.UserId == userId);
                if (account == null)
                {
                    _logger.LogWarning("Account not found for AccountId: {AccountId} and UserId: {UserId}", SelectedAccountId, userId);
                    return Json(new { success = false, error = "Selected account does not exist or is not associated with your user." });
                }

                // Calculate total adjustments for the new transaction
                decimal newTotalFine = 0m;
                decimal newTotalAmount = 0m;

                // If updating, reverse the previous transaction's effect
                decimal previousTotalFine = 0m;
                decimal previousTotalAmount = 0m;
                var existingTransactions = _context.Transactions
                    .Where(t => t.BillNo == model.BillNo && t.UserId == userId)
                    .ToList();

                if (existingTransactions.Any())
                {
                    foreach (var existingItem in existingTransactions)
                    {
                        string groupName = itemGroups[existingItem.ItemId!.Value];
                        CalculateDerivedFields(existingItem, groupName);

                        decimal fineAdjustment = existingItem.Fine ?? 0m;
                        decimal amountAdjustment = existingItem.Amount ?? 0m;
                        if (model.TransactionType == "Sale" || model.TransactionType == "PurchaseReturn")
                        {
                            fineAdjustment = -fineAdjustment;
                            amountAdjustment = -amountAdjustment;
                        }

                        previousTotalFine += fineAdjustment;
                        previousTotalAmount += amountAdjustment;
                    }

                    _context.Transactions.RemoveRange(existingTransactions);
                }

                // Process the new transaction items
                foreach (var item in model.Items!)
                {
                    item.AccountId = SelectedAccountId;
                    item.TransactionType = model.TransactionType;
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.LastUpdated = DateTime.Now;
                    item.UserId = userId;

                    string groupName = itemGroups[item.ItemId!.Value];
                    CalculateDerivedFields(item, groupName);

                    decimal fineAdjustment = item.Fine ?? 0m;
                    decimal amountAdjustment = item.Amount ?? 0m;
                    if (model.TransactionType == "Sale" || model.TransactionType == "PurchaseReturn")
                    {
                        fineAdjustment = -fineAdjustment;
                        amountAdjustment = -amountAdjustment;
                    }

                    newTotalFine += fineAdjustment;
                    newTotalAmount += amountAdjustment;

                    _context.Transactions.Add(item);
                }

                // Adjust the account balance
                account.Fine = account.Fine - previousTotalFine + newTotalFine;
                account.Amount = account.Amount - previousTotalAmount + newTotalAmount;

                // Update LastUpdated since Fine or Amount has changed
                account.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction saved successfully for BillNo: {BillNo}", model.BillNo);

                string redirectUrl = Url.Action("ViewStock", "OpeningStock")!;
                return Json(new { success = true, redirectUrl, message = "Transaction saved successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transaction for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(string billNo)
        {
            if (string.IsNullOrEmpty(billNo))
            {
                _logger.LogWarning("DeleteTransaction - Bill number is required.");
                return Json(new { success = false, error = "Bill number is required." });
            }

            var userId = GetCurrentUserId();

            try
            {
                var transactions = _context.Transactions
                    .Where(t => t.BillNo == billNo && t.UserId == userId)
                    .ToList();

                if (!transactions.Any())
                {
                    _logger.LogWarning("DeleteTransaction - No transaction found for BillNo: {BillNo} and UserId: {UserId}", billNo, userId);
                    return Json(new { success = false, error = "No transaction found with this BillNo or you do not have access to it." });
                }

                // Calculate the total adjustments to reverse
                decimal totalFineToReverse = 0m;
                decimal totalAmountToReverse = 0m;
                int? accountId = transactions.First().AccountId;

                var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);
                foreach (var transaction in transactions)
                {
                    string groupName = itemGroups[transaction.ItemId!.Value];
                    CalculateDerivedFields(transaction, groupName);

                    decimal fineAdjustment = transaction.Fine ?? 0m;
                    decimal amountAdjustment = transaction.Amount ?? 0m;
                    if (transaction.TransactionType == "Sale" || transaction.TransactionType == "PurchaseReturn")
                    {
                        fineAdjustment = -fineAdjustment;
                        amountAdjustment = -amountAdjustment;
                    }

                    totalFineToReverse += fineAdjustment;
                    totalAmountToReverse += amountAdjustment;
                }

                // Reverse the adjustments on the account
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);
                if (account != null)
                {
                    account.Fine -= totalFineToReverse;
                    account.Amount -= totalAmountToReverse;
                    account.LastUpdated = DateTime.Now;
                }

                // Remove the transactions
                _context.Transactions.RemoveRange(transactions);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction deleted successfully for BillNo: {BillNo}", billNo);

                string redirectUrl = Url.Action("ViewStock", "OpeningStock")!;
                return Json(new { success = true, redirectUrl, message = "Transaction deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction for BillNo: {BillNo}", billNo);
                return Json(new { success = false, error = $"An error occurred while deleting: {ex.Message}" });
            }
        }
    }
}