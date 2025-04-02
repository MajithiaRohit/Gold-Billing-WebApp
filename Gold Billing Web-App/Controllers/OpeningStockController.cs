using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Gold_Billing_Web_App.Session;
using System.Security.Claims;
using System.Data;

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

        private string GenerateSequentialBillNo()
        {
            var userId = GetCurrentUserId();
            const string prefix = "OS";
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
                    Id = (int)i.Id, // Remove !.Value since Id is int
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
        public IActionResult AddOpeningStock(string? billNo = null)
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
                model.BillNo = GenerateSequentialBillNo();
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

            // Validation logic remains unchanged
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
                model.BillNo = GenerateSequentialBillNo();
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
            }
            else
            {
                foreach (var item in model.Items)
                {
                    if (item.ItemId == 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Item is required.");
                        continue;
                    }

                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Selected item does not exist or is not associated with your user.");
                        continue;
                    }

                    string groupName = itemGroups.ContainsKey(item.ItemId) ? itemGroups[item.ItemId] : "";
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
                _logger.LogWarning("Validation Errors: {Errors}", string.Join("; ", errors));
                return View(model);
            }

            try
            {
                var existingStocks = _context.OpeningStocks
                    .Where(t => t.BillNo == model.BillNo && t.UserId == userId)
                    .ToList();
                if (existingStocks.Any())
                {
                    _context.OpeningStocks.RemoveRange(existingStocks);
                }

                foreach (var item in model.Items!)
                {
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.UserId = userId;
                    string groupName = itemGroups[item.ItemId];
                    CalculateDerivedFields(item, groupName);
                    _context.OpeningStocks.Add(item);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Opening stock saved successfully for BillNo: {BillNo}", model.BillNo);
                // Return JSON with redirect URL instead of direct redirect
                return Json(new { success = true, redirectUrl = Url.Action("ViewStock", "OpeningStock") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving opening stock for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = "An error occurred while saving." });
            }
        }
        public IActionResult ViewStock(int page = 1)
        {
            var userId = GetCurrentUserId();
            const int pageSize = 10; // Adjust as needed

            // Query the OpeningStocks table and join with Items for ItemName
            var stockQuery = from os in _context.OpeningStocks
                             join i in _context.Items on os.ItemId equals i.Id
                             where os.UserId == userId
                             select new
                             {
                                 os.BillNo,
                                 ItemName = i.Name,
                                 os.Pc,
                                 os.Weight,
                                 os.Less,
                                 os.NetWt,
                                 os.Tunch,
                                 os.Wastage,
                                 os.Fine,
                                 os.Amount,
                                 LastUpdated = os.Date // Assuming Date is the last updated field
                             };

            // Pagination
            var totalRecords = stockQuery.Count();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            var stocks = stockQuery
                .OrderBy(s => s.BillNo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Convert to DataTable
            DataTable dt = new DataTable();
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
                    stock.BillNo,
                    stock.ItemName,
                    stock.Pc,
                    stock.Weight,
                    stock.Less,
                    stock.NetWt,
                    stock.Tunch,
                    stock.Wastage,
                    stock.Fine,
                    stock.Amount,
                    stock.LastUpdated
                );
            }

            // Pass pagination data to the view
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(dt); // Pass the DataTable as the model
        }
    }
}