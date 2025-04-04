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
                _logger.LogWarning("UserId not found in session or claims. Session: {SessionValue}, Claims: {ClaimsValue}",
                    HttpContext.Session.GetString(CommonVariable.UserId), User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                userId = 1; // Temporary fallback
                _logger.LogInformation("Using default UserId: {UserId}", userId);
            }
            return userId;
        }

        private List<AccountDropDownModel> SetAccountDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.Accounts
                .Where(a => a.UserId == userId)
                .Select(a => new AccountDropDownModel { Id = a.AccountId, AccountName = a.AccountName })
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
            if (!string.IsNullOrEmpty(lastBill) && int.TryParse(lastBill.Substring(prefix.Length), out lastNumber))
            {
                lastNumber++;
            }
            return $"{prefix}{lastNumber:D4}";
        }

        private List<ItemDropDownModel> SetItemDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.Items
                .Include(i => i.ItemGroup)
                .Where(i => i.UserId == userId && i.ItemGroup!.Name != "Gold Metal")
                .Select(i => new ItemDropDownModel { Id = i.Id!.Value, ItemName = i.Name!, GroupName = i.ItemGroup!.Name ?? "" })
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
                .Select(a => new { Fine = a.Fine, Amount = a.Amount, LastUpdated = a.LastUpdated })
                .FirstOrDefault();

            return Json(account != null
                ? new { fine = account.Fine, amount = account.Amount, date = account.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss") }
                : new { fine = 0.0m, amount = 0.0m, date = (string?)null });
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
                var transactions = _context.Transactions.Where(t => t.BillNo == billNo && t.UserId == userId).ToList();
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

        // In TransactionController.cs, update the AddTransaction POST action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTransaction(TransactionViewModel model, int? SelectedAccountId)
        {
            _logger.LogInformation("POST - Attempting to save transaction for BillNo: {BillNo}", model.BillNo);

            var userId = GetCurrentUserId();
            model.UserId = userId;

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            if (string.IsNullOrEmpty(model.TransactionType))
            {
                model.TransactionType = Request.Form["TransactionType"].FirstOrDefault() ?? "Purchase";
                _logger.LogInformation("TransactionType not provided in model, set to: {TransactionType}", model.TransactionType);
            }

            if (string.IsNullOrEmpty(model.BillNo))
            {
                model.BillNo = GenerateSequentialBillNo(model.TransactionType);
                _logger.LogInformation("BillNo not provided, generated: {BillNo}", model.BillNo);
            }

            if (!SelectedAccountId.HasValue || model.Items == null || !model.Items.Any())
            {
                _logger.LogWarning("Validation failed: Account or items missing. SelectedAccountId: {SelectedAccountId}, Items: {ItemCount}",
                    SelectedAccountId, model.Items?.Count ?? 0);
                return Json(new { success = false, error = "Account and at least one item are required." });
            }

            foreach (var item in model.Items)
            {
                if (!item.ItemId.HasValue || !await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId))
                {
                    ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Invalid item.");
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid: {Errors}", string.Join("; ", errors));
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                var account = await _context.Accounts
                    .Include(a => a.GroupAccount) // Include GroupAccount if needed for group-specific logic
                    .FirstOrDefaultAsync(a => a.AccountId == SelectedAccountId && a.UserId == userId);
                if (account == null)
                {
                    _logger.LogWarning("Invalid account for AccountId: {AccountId}", SelectedAccountId);
                    return Json(new { success = false, error = "Invalid account." });
                }

                var existingTransactions = _context.Transactions.Where(t => t.BillNo == model.BillNo && t.UserId == userId).ToList();
                if (existingTransactions.Any())
                {
                    _logger.LogInformation("Updating existing transaction with BillNo: {BillNo}", model.BillNo);
                    foreach (var existingItem in existingTransactions)
                    {
                        await UpdateStockOnDelete(existingItem);
                    }
                    _context.Transactions.RemoveRange(existingTransactions);
                }

                // Calculate total Fine and Amount for the transaction
                decimal totalFine = 0m;
                decimal totalAmount = 0m;

                foreach (var item in model.Items)
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

                    _context.Transactions.Add(item);
                    await UpdateStock(item);

                    // Accumulate totals
                    totalFine += item.Fine ?? 0;
                    totalAmount += item.Amount ?? 0;
                }

                // Update account balance based on transaction type
                switch (model.TransactionType)
                {
                    case "Purchase":
                    case "SaleReturn":
                        account.Fine += totalFine;
                        account.Amount += totalAmount;
                        break;
                    case "Sale":
                    case "PurchaseReturn":
                        account.Fine -= totalFine;
                        account.Amount -= totalAmount;
                        break;
                }
                account.LastUpdated = DateTime.Now;

                // Ensure account changes are saved
                _context.Accounts.Update(account);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction saved successfully for BillNo: {BillNo}, Account updated: Fine={Fine}, Amount={Amount}",
                    model.BillNo, account.Fine, account.Amount);

                return Json(new { success = true, redirectUrl = Url.Action("ViewStock", "OpeningStock"), message = "Transaction saved successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transaction for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = $"An error occurred: {ex.Message}" });
            }
        }

        private async Task UpdateStock(TransactionModel item)
        {
            var stock = await _context.OpeningStocks.FirstOrDefaultAsync(s => s.ItemId == item.ItemId && s.UserId == item.UserId);
            if (stock == null)
            {
                stock = new OpeningStockModel
                {
                    ItemId = item.ItemId!.Value,
                    UserId = item.UserId,
                    BillNo = item.BillNo, // Use transaction BillNo instead of "Opening"
                    Date = item.Date,     // Required field, use transaction date
                    StockType = item.TransactionType, // Set to "Purchase", "Sale", etc.
                    LastUpdated = DateTime.Now
                };
                _context.OpeningStocks.Add(stock);
            }

            switch (item.TransactionType)
            {
                case "Purchase":
                case "SaleReturn":
                    stock.Pc = (stock.Pc ?? 0) + (item.Pc ?? 0);
                    stock.Weight = (stock.Weight ?? 0) + (item.Weight ?? 0);
                    stock.Less = (stock.Less ?? 0) + (item.Less ?? 0);
                    stock.NetWt = (stock.NetWt ?? 0) + (item.NetWt ?? 0);
                    stock.Tunch = item.Tunch ?? stock.Tunch;
                    stock.Wastage = item.Wastage ?? stock.Wastage;
                    stock.Fine = (stock.Fine ?? 0) + (item.Fine ?? 0);
                    stock.Amount = (stock.Amount ?? 0) + (item.Amount ?? 0);
                    break;
                case "Sale":
                case "PurchaseReturn":
                    stock.Pc = (stock.Pc ?? 0) - (item.Pc ?? 0);
                    stock.Weight = (stock.Weight ?? 0) - (item.Weight ?? 0);
                    stock.Less = (stock.Less ?? 0) - (item.Less ?? 0);
                    stock.NetWt = (stock.NetWt ?? 0) - (item.NetWt ?? 0);
                    stock.Tunch = item.Tunch ?? stock.Tunch;
                    stock.Wastage = item.Wastage ?? stock.Wastage;
                    stock.Fine = (stock.Fine ?? 0) - (item.Fine ?? 0);
                    stock.Amount = (stock.Amount ?? 0) - (item.Amount ?? 0);
                    break;
            }
            stock.LastUpdated = DateTime.Now;
            if (stock.Date == default) // Ensure Date is always set
            {
                stock.Date = item.Date;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Stock updated for ItemId: {ItemId}, UserId: {UserId}, NetWt: {NetWt}, StockType: {StockType}",
                stock.ItemId, stock.UserId, stock.NetWt, stock.StockType);
        }

        private async Task UpdateStockOnDelete(TransactionModel item)
        {
            var stock = await _context.OpeningStocks.FirstOrDefaultAsync(s => s.ItemId == item.ItemId && s.UserId == item.UserId);
            if (stock != null)
            {
                switch (item.TransactionType)
                {
                    case "Purchase":
                    case "SaleReturn":
                        stock.Pc = (stock.Pc ?? 0) - (item.Pc ?? 0);
                        stock.Weight = (stock.Weight ?? 0) - (item.Weight ?? 0);
                        stock.Less = (stock.Less ?? 0) - (item.Less ?? 0);
                        stock.NetWt = (stock.NetWt ?? 0) - (item.NetWt ?? 0);
                        stock.Fine = (stock.Fine ?? 0) - (item.Fine ?? 0);
                        stock.Amount = (stock.Amount ?? 0) - (item.Amount ?? 0);
                        break;
                    case "Sale":
                    case "PurchaseReturn":
                        stock.Pc = (stock.Pc ?? 0) + (item.Pc ?? 0);
                        stock.Weight = (stock.Weight ?? 0) + (item.Weight ?? 0);
                        stock.Less = (stock.Less ?? 0) + (item.Less ?? 0);
                        stock.NetWt = (stock.NetWt ?? 0) + (item.NetWt ?? 0);
                        stock.Fine = (stock.Fine ?? 0) + (item.Fine ?? 0);
                        stock.Amount = (stock.Amount ?? 0) + (item.Amount ?? 0);
                        break;
                }
                stock.LastUpdated = DateTime.Now;
                if (stock.NetWt <= 0 && stock.Fine <= 0) _context.OpeningStocks.Remove(stock);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Stock updated on delete for ItemId: {ItemId}, UserId: {UserId}", stock.ItemId, stock.UserId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(string billNo)
        {
            var userId = GetCurrentUserId();
            var transactions = _context.Transactions.Where(t => t.BillNo == billNo && t.UserId == userId).ToList();

            if (!transactions.Any()) return Json(new { success = false, error = "Transaction not found." });

            try
            {
                var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);
                foreach (var transaction in transactions)
                {
                    string groupName = itemGroups[transaction.ItemId!.Value];
                    CalculateDerivedFields(transaction, groupName);
                    await UpdateStockOnDelete(transaction);
                }

                _context.Transactions.RemoveRange(transactions);
                await _context.SaveChangesAsync();
                return Json(new { success = true, redirectUrl = Url.Action("ViewStock", "OpeningStock"), message = "Transaction deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction for BillNo: {BillNo}", billNo);
                return Json(new { success = false, error = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult StockDetails(int itemId)
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Loading StockDetails for ItemId: {ItemId}, UserId: {UserId}", itemId, userId);

            var stock = _context.OpeningStocks
                .Include(s => s.Item)
                .FirstOrDefault(s => s.ItemId == itemId && s.UserId == userId);

            var transactions = _context.Transactions
                .Include(t => t.Item)
                .Where(t => t.ItemId == itemId && t.UserId == userId)
                .OrderBy(t => t.Date)
                .ToList();

            _logger.LogInformation("Found {StockCount} stock record and {TransactionCount} transactions for ItemId: {ItemId}",
                stock != null ? 1 : 0, transactions.Count, itemId);

            ViewBag.Transactions = transactions;

            if (stock == null)
            {
                stock = new OpeningStockModel
                {
                    ItemId = itemId,
                    UserId = userId,
                    Item = _context.Items.FirstOrDefault(i => i.Id == itemId)
                };
                _logger.LogWarning("No stock found for ItemId: {ItemId}, returning default stock object", itemId);
            }

            return View(stock);
        }
    }
}