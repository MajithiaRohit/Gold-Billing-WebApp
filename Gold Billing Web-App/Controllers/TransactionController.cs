using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gold_Billing_Web_App.Controllers
{
    public class TransactionController : Controller
    {
        private readonly AppDbContext _context;

        public TransactionController(AppDbContext context)
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

        private List<AccountDropDownModel> SetAccountDropDown()
        {
            var userId = GetCurrentUserId();
            return _context.Accounts
                .Where(a => a.UserId == userId) // Filter by UserId
                .Select(a => new AccountDropDownModel
                {
                    Id = a.AccountId!.Value,
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
            return _context.Transactions.Any(t => t.BillNo == billNo && t.UserId == userId); // Filter by UserId
        }
        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            var userId = GetCurrentUserId();
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId && a.UserId == userId) // Filter by UserId
                .Select(a => new
                {
                    Fine = a.Fine,
                    Amount = a.Amount,
                    Date = a.Date
                })
                .FirstOrDefault();

            if (account != null)
            {
                return Json(new
                {
                    fine = account.Fine,
                    amount = account.Amount,
                    date = account.Date.ToString("yyyy-MM-dd")
                });
            }
            return Json(new { fine = 0.0, amount = 0.0, date = (string?)null });
        }

        [HttpGet]
        public IActionResult AddTransaction(string type, int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Purchase", "Sale", "PurchaseReturn", "SaleReturn" }.Contains(type))
            {
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
                    .Include(t => t.Account)
                    .Include(t => t.Item)
                    .ToList();

                if (!transactions.Any())
                {
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

            // Debug: Log the BillNo before rendering the view
            Console.WriteLine($"GET - BillNo set to: {model.BillNo}");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTransaction(TransactionViewModel model, int? SelectedAccountId)
        {
            var userId = GetCurrentUserId();
            model.UserId = userId;

            Console.WriteLine($"POST - Received BillNo: {model.BillNo}");
            Console.WriteLine($"POST - Received Date: {model.Date}");
            if (model.Items != null)
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    Console.WriteLine($"POST - Items[{i}].BillNo: {model.Items[i].BillNo}");
                    Console.WriteLine($"POST - Items[{i}].Date: {model.Items[i].Date}");
                }
            }

            // Log the raw form data
            var formData = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
            Console.WriteLine("POST - Form Data:");
            foreach (var kvp in formData)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            if (string.IsNullOrEmpty(model.TransactionType))
            {
                model.TransactionType = Request.Form["TransactionType"].FirstOrDefault() ?? "Purchase";
            }

            if (string.IsNullOrEmpty(model.BillNo))
            {
                model.BillNo = GenerateSequentialBillNo(model.TransactionType);
                Console.WriteLine($"POST - Generated new BillNo: {model.BillNo}");
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
                Console.WriteLine($"POST - Validation Errors: {string.Join("; ", errors)}");
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                decimal totalFine = 0m;
                decimal totalAmount = 0m;

                var existingTransactions = _context.Transactions
                    .Where(t => t.BillNo == model.BillNo && t.UserId == userId);
                if (existingTransactions.Any())
                {
                    _context.Transactions.RemoveRange(existingTransactions);
                }

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

                    totalFine += fineAdjustment;
                    totalAmount += amountAdjustment;

                    _context.Transactions.Add(item);
                }

                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == SelectedAccountId && a.UserId == userId);
                if (account != null)
                {
                    account.Fine += totalFine;
                    account.Amount += totalAmount;
                }

                await _context.SaveChangesAsync();

                string redirectUrl = Url.Action("ViewStock", "OpeningStock")!;
                return Json(new { success = true, redirectUrl, message = "Transaction saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(string billNo)
        {
            if (string.IsNullOrEmpty(billNo))
            {
                return Json(new { success = false, error = "Bill number is required." });
            }

            var userId = GetCurrentUserId();

            try
            {
                var transactions = _context.Transactions
                    .Where(t => t.BillNo == billNo && t.UserId == userId); // Filter by UserId
                if (!transactions.Any())
                {
                    return Json(new { success = false, error = "No transaction found with this BillNo or you do not have access to it." });
                }

                _context.Transactions.RemoveRange(transactions);
                await _context.SaveChangesAsync();

                string redirectUrl = Url.Action("ViewStock", "OpeningStock")!;
                return Json(new { success = true, redirectUrl, message = "Transaction deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"An error occurred while deleting: {ex.Message}" });
            }
        }
    }
}