using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Gold_Billing_Web_App.Controllers
{
    public class MetalTransectionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MetalTransectionController> _logger;

        public MetalTransectionController(AppDbContext context, ILogger<MetalTransectionController> logger)
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
                .Where(a => a.UserId == userId)
                .Include(a => a.GroupAccount)
                .Select(a => new AccountDropDownModel
                {
                    Id = (int)a.AccountId,
                    AccountName = a.AccountName,
                    GroupName = a.GroupAccount != null ? a.GroupAccount.GroupName : ""
                })
                .ToList();
        }

        private List<ItemDropDownModel> SetItemDropDown()
        {
            var userId = GetCurrentUserId();
            var allowedItems = new List<string> { "Fine Metal", "Cadbury", "Dhal" };
            return _context.Items
                .Where(i => allowedItems.Contains(i.Name) && i.UserId == userId)
                .Select(i => new ItemDropDownModel
                {
                    Id = (int)i.Id,
                    ItemName = i.Name
                })
                .ToList();
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string type)
        {
            if (!new[] { "Payment", "Receipt" }.Contains(type))
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

        private string GenerateSequentialBillNo(string transactionType)
        {
            var userId = GetCurrentUserId();
            string prefix = transactionType == "Payment" ? "PAYM" : "RECV";
            var lastBill = _context.MetalTransactions
                .Where(t => t.BillNo!.StartsWith(prefix) && t.UserId == userId)
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
        public IActionResult GetPreviousBalance(int accountId)
        {
            var userId = GetCurrentUserId();
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId && a.UserId == userId)
                .Select(a => new { a.Fine, a.Amount })
                .FirstOrDefault();

            return Json(account != null ? new { fine = account.Fine, amount = account.Amount } : new { fine = 0.0, amount = 0.0 });
        }

        public IActionResult GenrateMetalTransectionVoucher(string type = "Payment", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Payment", "Receipt" }.Contains(type))
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new MetalTransactionViewModel { UserId = userId };

            bool isExistingTransaction = !string.IsNullOrEmpty(billNo) && _context.MetalTransactions.Any(t => t.BillNo == billNo && t.UserId == userId);
            ViewBag.IsExistingTransaction = isExistingTransaction;

            if (isExistingTransaction)
            {
                var transactions = _context.MetalTransactions
                    .Where(t => t.BillNo == billNo && t.UserId == userId)
                    .Include(t => t.Account)
                    .Include(t => t.Item)
                    .ToList();

                if (!transactions.Any())
                {
                    return NotFound();
                }

                model.BillNo = billNo;
                model.Date = transactions.First().Date;
                model.Narration = transactions.First().Narration;
                model.Type = type;
                model.Items = transactions;
                model.UserId = userId;
                model.SelectedAccountId = transactions.First().AccountId;
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                model.Date = DateTime.Now;
                model.Type = type;
                model.Items = new List<MetalTransactionModel> { new MetalTransactionModel { Type = type, UserId = userId } };
                model.UserId = userId;
                model.SelectedAccountId = accountId;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMetalTransaction(MetalTransactionViewModel model)
        {
            var userId = GetCurrentUserId();
            model.UserId = userId;

            _logger.LogInformation("Received POST request for AddMetalTransaction");
            _logger.LogInformation("Model - SelectedAccountId: {SelectedAccountId}, Items Count: {ItemsCount}",
                model.SelectedAccountId, model.Items?.Count ?? 0);
            if (model.Items != null)
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    _logger.LogInformation("Items[{Index}]: ItemId={ItemId}, GrossWeight={GrossWeight}, Tunch={Tunch}",
                        i, model.Items[i].ItemId, model.Items[i].GrossWeight, model.Items[i].Tunch);
                }
            }
            _logger.LogInformation("Form data: {@Form}", Request.Form);

            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.ItemDropDown = SetItemDropDown();

            model.Type ??= Request.Form["Type"].FirstOrDefault() ?? "Payment";

            // Validation
            if (!model.SelectedAccountId.HasValue || model.SelectedAccountId == 0)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
                _logger.LogWarning("Validation failed: SelectedAccountId is null or 0");
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
                _logger.LogWarning("Validation failed: Items is null or empty");
            }
            else
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    var item = model.Items[i];
                    if (!item.ItemId.HasValue || item.ItemId == 0)
                    {
                        ModelState.AddModelError($"Items[{i}].ItemId", "Item is required.");
                        _logger.LogWarning("Validation failed: Items[{Index}].ItemId is null or 0", i);
                    }
                    if (!item.GrossWeight.HasValue || item.GrossWeight <= 0)
                    {
                        ModelState.AddModelError($"Items[{i}].GrossWeight", "Gross Weight must be greater than 0.");
                        _logger.LogWarning("Validation failed: Items[{Index}].GrossWeight is invalid", i);
                    }
                    if (!item.Tunch.HasValue || item.Tunch <= 0)
                    {
                        ModelState.AddModelError($"Items[{i}].Tunch", "Tunch must be greater than 0.");
                        _logger.LogWarning("Validation failed: Items[{Index}].Tunch is invalid", i);
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid. Errors: {Errors}", string.Join("; ", errors));
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                decimal totalFine = 0;
                var existingTransactions = _context.MetalTransactions.Where(t => t.BillNo == model.BillNo && t.UserId == userId);
                if (existingTransactions.Any())
                {
                    _context.MetalTransactions.RemoveRange(existingTransactions);
                }

                foreach (var item in model.Items)
                {
                    item.AccountId = model.SelectedAccountId;
                    item.Type = model.Type;
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.UserId = userId;

                    item.Fine = (item.GrossWeight ?? 0) * (item.Tunch ?? 0) / 100;

                    decimal fineAdjustment = item.Fine ?? 0;
                    if (model.Type == "Payment")
                    {
                        fineAdjustment = -fineAdjustment;
                    }

                    totalFine += fineAdjustment;

                    _context.MetalTransactions.Add(item);
                }

                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == model.SelectedAccountId && a.UserId == userId);

                if (account != null)
                {
                    account.Fine += totalFine;
                    account.Amount += totalFine; // Assuming Amount is updated similarly; adjust if needed
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