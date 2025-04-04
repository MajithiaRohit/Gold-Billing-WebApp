using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Data;
using Gold_Billing_Web_App.Session;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OpeningStockController> _logger;

        public OpeningStockController(AppDbContext context, ILogger<OpeningStockController> logger)
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

        private string GenerateSequentialBillNo(string prefix)
        {
            var userId = GetCurrentUserId();
            var lastBill = _context.OpeningStocks
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
                .Where(i => i.UserId == userId)
                .Select(i => new ItemDropDownModel
                {
                    Id = (int)i.Id,
                    ItemName = i.Name!,
                    GroupName = i.ItemGroup!.Name ?? ""
                })
                .ToList();
        }

        private void CalculateDerivedFields(OpeningStockModel model, string groupName)
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

        [HttpGet]
        public IActionResult AddOpeningStock(string? billNo = null, bool isNewStock = false)
        {
            var userId = GetCurrentUserId();
            var itemDropDown = SetItemDropDown();
            ViewBag.ItemDropDown = itemDropDown;

            var model = new OpeningStockViewModel { UserId = userId };
            bool isExisting = !string.IsNullOrEmpty(billNo) && _context.OpeningStocks.Any(t => t.BillNo == billNo && t.UserId == userId);
            ViewBag.IsExisting = isExisting;

            if (isExisting)
            {
                var stocks = _context.OpeningStocks
                    .Where(t => t.BillNo == billNo && t.UserId == userId)
                    .ToList();
                if (!stocks.Any()) return NotFound();

                model.BillNo = billNo!;
                model.Date = stocks.First().Date;
                model.Narration = stocks.First().Narration;
                model.Items = stocks;
            }
            else
            {
                bool hasOpeningStock = _context.OpeningStocks.Any(t => t.UserId == userId && t.StockType == "Opening");
                if (!isNewStock && hasOpeningStock)
                {
                    TempData["Error"] = "You have already added opening stock. Please use Edit or Add New Stock.";
                    return RedirectToAction("ViewStock");
                }

                model.BillNo = isNewStock || hasOpeningStock ? GenerateSequentialBillNo("NS") : GenerateSequentialBillNo("OS");
                model.Date = DateTime.Now;
                model.Items = new List<OpeningStockModel> { new OpeningStockModel { UserId = userId } };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOpeningStock(OpeningStockViewModel model)
        {
            var userId = GetCurrentUserId();
            model.UserId = userId;

            ViewBag.ItemDropDown = SetItemDropDown();
            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            if (model.Items != null)
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    if (ModelState.ContainsKey($"Items[{i}].Item")) ModelState.Remove($"Items[{i}].Item");
                    if (ModelState.ContainsKey($"Items[{i}].User")) ModelState.Remove($"Items[{i}].User");
                }
            }

            if (string.IsNullOrEmpty(model.BillNo))
            {
                bool hasOpeningStock = _context.OpeningStocks.Any(t => t.UserId == userId && t.StockType == "Opening");
                model.BillNo = hasOpeningStock ? GenerateSequentialBillNo("NS") : GenerateSequentialBillNo("OS");
            }

            if (model.Items == null || !model.Items.Any())
            {
                return Json(new { success = false, error = "At least one item is required." });
            }

            foreach (var item in model.Items)
            {
                if (item.ItemId == 0)
                {
                    return Json(new { success = false, error = $"Item is required for row {model.Items.IndexOf(item) + 1}." });
                }

                var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                if (!itemExists)
                {
                    return Json(new { success = false, error = $"Selected item does not exist for row {model.Items.IndexOf(item) + 1}." });
                }

                string groupName = itemGroups[item.ItemId];
                item.StockType = model.BillNo.StartsWith("OS") ? "Opening" : "New";
                switch (groupName)
                {
                    case "Gold Jewelry":
                        if (!item.Weight.HasValue || item.Weight <= 0) return Json(new { success = false, error = $"Gross Weight is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Tunch.HasValue || item.Tunch <= 0) return Json(new { success = false, error = $"Tunch is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Wastage.HasValue || item.Wastage <= 0) return Json(new { success = false, error = $"Wastage is required for row {model.Items.IndexOf(item) + 1}." });
                        break;
                    case "PC Gold Jewelry":
                        if (!item.Pc.HasValue || item.Pc <= 0) return Json(new { success = false, error = $"Pc is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Rate.HasValue || item.Rate <= 0) return Json(new { success = false, error = $"Rate is required for row {model.Items.IndexOf(item) + 1}." });
                        break;
                    case "PC/Weight Jewelry":
                        if (!item.Pc.HasValue || item.Pc <= 0) return Json(new { success = false, error = $"Pc is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Weight.HasValue || item.Weight <= 0) return Json(new { success = false, error = $"Gross Weight is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Tunch.HasValue || item.Tunch <= 0) return Json(new { success = false, error = $"Tunch is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Wastage.HasValue || item.Wastage <= 0) return Json(new { success = false, error = $"Wastage is required for row {model.Items.IndexOf(item) + 1}." });
                        if (!item.Rate.HasValue || item.Rate <= 0) return Json(new { success = false, error = $"Rate is required for row {model.Items.IndexOf(item) + 1}." });
                        break;
                    default:
                        return Json(new { success = false, error = $"Invalid item group for row {model.Items.IndexOf(item) + 1}." });
                }
            }

            try
            {
                var existingStocks = _context.OpeningStocks
                    .Where(t => t.BillNo == model.BillNo && t.UserId == userId)
                    .ToList();
                if (existingStocks.Any())
                {
                    _context.OpeningStocks.RemoveRange(existingStocks);
                    await _context.SaveChangesAsync();
                }

                bool isOpeningStock = model.BillNo.StartsWith("OS");
                if (isOpeningStock && _context.OpeningStocks.Any(t => t.UserId == userId && t.StockType == "Opening" && t.BillNo != model.BillNo))
                {
                    return Json(new { success = false, error = "Opening stock can only be added once. Use New Stock instead." });
                }

                foreach (var item in model.Items)
                {
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.UserId = userId;
                    item.StockType = isOpeningStock ? "Opening" : "New";
                    item.LastUpdated = DateTime.Now;
                    string groupName = itemGroups[item.ItemId];
                    CalculateDerivedFields(item, groupName);
                    _context.OpeningStocks.Add(item);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Stock saved successfully for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = true, redirectUrl = Url.Action("ViewStock", "OpeningStock") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving stock for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = "An error occurred while saving: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult AddNewStock()
        {
            return RedirectToAction("AddOpeningStock", new { isNewStock = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOpeningStock(string billNo)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(billNo))
                {
                    return Json(new { success = false, error = "Bill number is required." });
                }

                var stocksToDelete = _context.OpeningStocks
                    .Where(t => t.BillNo == billNo && t.UserId == userId)
                    .ToList();

                if (!stocksToDelete.Any())
                {
                    return Json(new { success = false, error = "Bill not found." });
                }

                _context.OpeningStocks.RemoveRange(stocksToDelete);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Stock deleted successfully for BillNo: {BillNo}", billNo);
                return Json(new { success = true, redirectUrl = Url.Action("ViewStock", "OpeningStock") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting stock for BillNo: {BillNo}", billNo);
                return Json(new { success = false, error = "An error occurred while deleting: " + ex.Message });
            }
        }

        public IActionResult ViewStock()
        {
            var userId = GetCurrentUserId();

            var stockQuery = from i in _context.Items
                             where i.UserId == userId
                             join os in _context.OpeningStocks on i.Id equals os.ItemId into osGroup
                             from os in osGroup.DefaultIfEmpty()
                             join t in _context.Transactions on i.Id equals t.ItemId into tGroup
                             from t in tGroup.DefaultIfEmpty()
                             group new { os, t } by new { i.Id, i.Name } into g
                             select new
                             {
                                 ItemId = g.Key.Id,
                                 ItemName = g.Key.Name,
                                 Pc = g.Sum(x => x.os != null ? (x.os.Pc ?? 0) : 0) + g.Sum(x => x.t != null ? (x.t.TransactionType == "Sale" || x.t.TransactionType == "PurchaseReturn" ? -(x.t.Pc ?? 0) : (x.t.Pc ?? 0)) : 0),
                                 Weight = g.Sum(x => x.os != null ? (x.os.Weight ?? 0) : 0) + g.Sum(x => x.t != null ? (x.t.TransactionType == "Sale" || x.t.TransactionType == "PurchaseReturn" ? -(x.t.Weight ?? 0) : (x.t.Weight ?? 0)) : 0),
                                 Less = g.Where(x => x.os != null).OrderByDescending(x => x.os.Date).Select(x => x.os.Less).FirstOrDefault() ?? g.Where(x => x.t != null).OrderByDescending(x => x.t.Date).Select(x => x.t.Less).FirstOrDefault(),
                                 NetWt = g.Sum(x => x.os != null ? (x.os.NetWt ?? 0) : 0) + g.Sum(x => x.t != null ? (x.t.TransactionType == "Sale" || x.t.TransactionType == "PurchaseReturn" ? -(x.t.NetWt ?? 0) : (x.t.NetWt ?? 0)) : 0),
                                 Tunch = g.Where(x => x.os != null).OrderByDescending(x => x.os.Date).Select(x => x.os.Tunch).FirstOrDefault() ?? g.Where(x => x.t != null).OrderByDescending(x => x.t.Date).Select(x => x.t.Tunch).FirstOrDefault(),
                                 Wastage = g.Where(x => x.os != null).OrderByDescending(x => x.os.Date).Select(x => x.os.Wastage).FirstOrDefault() ?? g.Where(x => x.t != null).OrderByDescending(x => x.t.Date).Select(x => x.t.Wastage).FirstOrDefault(),
                                 Fine = g.Sum(x => x.os != null ? (x.os.Fine ?? 0) : 0) + g.Sum(x => x.t != null ? (x.t.TransactionType == "Sale" || x.t.TransactionType == "PurchaseReturn" ? -(x.t.Fine ?? 0) : (x.t.Fine ?? 0)) : 0),
                                 Amount = g.Sum(x => x.os != null ? (x.os.Amount ?? 0) : 0) + g.Sum(x => x.t != null ? (x.t.TransactionType == "Sale" || x.t.TransactionType == "PurchaseReturn" ? -(x.t.Amount ?? 0) : (x.t.Amount ?? 0)) : 0),
                                 LastUpdated = g.Where(x => x.os != null).Select(x => (DateTime?)x.os.Date)
                                                .Concat(g.Where(x => x.t != null).Select(x => (DateTime?)x.t.Date))
                                                .DefaultIfEmpty()
                                                .Max()
                             };

            var stocks = stockQuery.OrderBy(s => s.ItemName).ToList();

            DataTable dt = new DataTable();
            dt.Columns.Add("ItemId", typeof(int));
            dt.Columns.Add("BillNo", typeof(string));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("Pc", typeof(int));
            dt.Columns.Add("Weight", typeof(decimal));
            dt.Columns.Add("Less", typeof(decimal));
            dt.Columns.Add("NetWt", typeof(decimal));
            dt.Columns.Add("Tunch", typeof(decimal));
            dt.Columns.Add("Wastage", typeof(decimal));
            dt.Columns.Add("Fine", typeof(decimal));
            dt.Columns.Add("Amount", typeof(decimal));
            dt.Columns.Add("LastUpdated", typeof(DateTime));

            foreach (var stock in stocks)
            {
                dt.Rows.Add(
                    stock.ItemId,
                    "",
                    stock.ItemName,
                    stock.Pc,
                    stock.Weight,
                    stock.Less,
                    stock.NetWt,
                    stock.Tunch,
                    stock.Wastage,
                    stock.Fine,
                    stock.Amount,
                    stock.LastUpdated.HasValue ? stock.LastUpdated.Value : DBNull.Value
                );
            }

            return View(dt);
        }

        [HttpGet]
        public IActionResult StockDetails(int itemId)
        {
            var userId = GetCurrentUserId();

            var stockItems = _context.OpeningStocks
                .Where(os => os.UserId == userId && os.ItemId == itemId)
                .Include(os => os.Item)
                .OrderBy(os => os.Date)
                .Select(os => new StockEntry
                {
                    BillNo = os.BillNo,
                    Type = os.StockType,
                    Sign = "+",
                    ItemName = os.Item.Name,
                    Pc = os.Pc,
                    Weight = os.Weight,
                    Less = os.Less,
                    NetWt = os.NetWt,
                    Tunch = os.Tunch,
                    Wastage = os.Wastage,
                    Fine = os.Fine,
                    Amount = os.Amount,
                    Date = os.Date
                })
                .ToList();

            var transactions = _context.Transactions
                .Where(t => t.UserId == userId && t.ItemId == itemId)
                .Include(t => t.Item)
                .OrderBy(t => t.Date)
                .Select(t => new StockEntry
                {
                    BillNo = t.BillNo,
                    Type = t.TransactionType,
                    Sign = t.TransactionType == "Purchase" || t.TransactionType == "SaleReturn" ? "+" : "-",
                    ItemName = t.Item.Name,
                    Pc = t.Pc,
                    Weight = t.Weight,
                    Less = t.Less,
                    NetWt = t.NetWt,
                    Tunch = t.Tunch,
                    Wastage = t.Wastage,
                    Fine = t.Fine,
                    Amount = t.Amount,
                    Date = t.Date
                })
                .ToList();

            var allEntries = stockItems.Concat(transactions)
                .OrderBy(e => e.Date)
                .ToList();

            if (!allEntries.Any())
            {
                var item = _context.Items.FirstOrDefault(i => i.Id == itemId && i.UserId == userId);
                if (item == null) return NotFound();

                var model = new StockDetailsViewModel
                {
                    ItemId = itemId,
                    ItemName = item.Name,
                    Entries = new List<StockEntry>(),
                    TotalPc = 0,
                    TotalWeight = 0,
                    TotalNetWt = 0,
                    TotalFine = 0,
                    TotalAmount = 0
                };
                return View(model);
            }

            var stockModel = new StockDetailsViewModel
            {
                ItemId = itemId,
                ItemName = allEntries.First().ItemName,
                Entries = allEntries,
                TotalPc = allEntries.Sum(e => e.Type == "Sale" || e.Type == "PurchaseReturn" ? -(e.Pc ?? 0) : (e.Pc ?? 0)),
                TotalWeight = allEntries.Sum(e => e.Type == "Sale" || e.Type == "PurchaseReturn" ? -(e.Weight ?? 0) : (e.Weight ?? 0)),
                TotalNetWt = allEntries.Sum(e => e.Type == "Sale" || e.Type == "PurchaseReturn" ? -(e.NetWt ?? 0) : (e.NetWt ?? 0)),
                TotalFine = allEntries.Sum(e => e.Type == "Sale" || e.Type == "PurchaseReturn" ? -(e.Fine ?? 0) : (e.Fine ?? 0)),
                TotalAmount = allEntries.Sum(e => e.Type == "Sale" || e.Type == "PurchaseReturn" ? -(e.Amount ?? 0) : (e.Amount ?? 0))
            };

            return View(stockModel);
        }

        [HttpGet]
        public IActionResult EditStock(string billNo)
        {
            return RedirectToAction("AddOpeningStock", new { billNo });
        }
    }
}