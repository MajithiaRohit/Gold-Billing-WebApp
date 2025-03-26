using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Data;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        private readonly AppDbContext _context;

        public OpeningStockController(AppDbContext context)
        {
            _context = context;
        }

        private string GenerateSequentialBillNo(string prefix = "BILL")
        {
            var lastBill = _context.OpeningStocks
                .Where(os => os.BillNo.StartsWith(prefix))
                .OrderByDescending(os => os.BillNo)
                .Select(os => os.BillNo)
                .FirstOrDefault();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastBill))
            {
                lastNumber = int.Parse(lastBill.Substring(prefix.Length));
            }
            return $"{prefix}{(lastNumber + 1):D4}";
        }

        private bool IsOpeningStockAdded()
        {
            return _context.OpeningStocks.Any();
        }

        private List<ItemDropDownModel> SetItemDropDown()
        {
            return _context.Items
                .Select(i => new ItemDropDownModel
                {
                    Id = i.Id!.Value,
                    ItemName = i.Name!
                })
                .ToList();
        }

        public IActionResult AddOpeningStock()
        {
            if (IsOpeningStockAdded())
            {
                TempData["Message"] = "Opening stock has already been added. Use 'New Stock' to add more.";
                return RedirectToAction("ViewStock");
            }
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new OpeningStockViewModel
            {
                BillNo = GenerateSequentialBillNo(),
                Date = DateTime.Now,
                Items = new List<OpeningStockModel> { new OpeningStockModel() }
            };
            ViewBag.Action = "AddOpeningStock";
            ViewBag.Title = "Add Opening Stock";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOpeningStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "AddOpeningStock";
                ViewBag.Title = "Add Opening Stock";
                return View(model);
            }

            try
            {
                foreach (var item in model.Items)
                {
                    CalculateDerivedFields(item);
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.LastUpdated = DateTime.Now;
                    _context.OpeningStocks.Add(item);
                }
                await _context.SaveChangesAsync();
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "AddOpeningStock";
                ViewBag.Title = "Add Opening Stock";
                return View(model);
            }
        }

        public IActionResult AddNewStock()
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new OpeningStockViewModel
            {
                BillNo = GenerateSequentialBillNo("NEW"),
                Date = DateTime.Now,
                Items = new List<OpeningStockModel> { new OpeningStockModel() }
            };
            ViewBag.Action = "AddNewStock";
            ViewBag.Title = "Add New Stock";
            return View("AddOpeningStock", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNewStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "AddNewStock";
                ViewBag.Title = "Add New Stock";
                return View("AddOpeningStock", model);
            }
            return await AddOpeningStock(model);
        }

        public IActionResult EditOpeningStock(string billNo)
        {
            if (string.IsNullOrEmpty(billNo))
            {
                return NotFound();
            }

            var stocks = _context.OpeningStocks
                .Where(os => os.BillNo == billNo)
                .Include(os => os.Item)
                .ToList();

            if (!stocks.Any())
            {
                return NotFound();
            }

            var model = new OpeningStockViewModel
            {
                BillNo = billNo,
                Date = stocks.First().Date,
                Narration = stocks.First().Narration,
                Items = stocks
            };

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "EditOpeningStock";
            ViewBag.Title = "Edit Stock";
            return View("AddOpeningStock", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOpeningStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "EditOpeningStock";
                ViewBag.Title = "Edit Stock";
                return View("AddOpeningStock", model);
            }

            try
            {
                var existingStocks = _context.OpeningStocks.Where(os => os.BillNo == model.BillNo);
                _context.OpeningStocks.RemoveRange(existingStocks);

                foreach (var item in model.Items)
                {
                    CalculateDerivedFields(item);
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.LastUpdated = DateTime.Now;
                    _context.OpeningStocks.Add(item);
                }
                await _context.SaveChangesAsync();
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "EditOpeningStock";
                ViewBag.Title = "Edit Stock";
                return View("AddOpeningStock", model);
            }
        }

        public IActionResult ViewStock(int page = 1)
        {
            const int pageSize = 10;

            var billNosQuery = _context.OpeningStocks
                .Select(os => os.BillNo)
                .Distinct()
                .OrderBy(billNo => billNo);

            var totalRecords = billNosQuery.Count();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            var paginatedBillNos = billNosQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var stocks = _context.OpeningStocks
                .Include(os => os.Item)
                .Where(os => paginatedBillNos.Contains(os.BillNo))
                .OrderBy(os => os.BillNo)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;

            DataTable table = new DataTable();
            table.Columns.Add("BillNo", typeof(string));
            table.Columns.Add("Date", typeof(DateTime));
            table.Columns.Add("ItemName", typeof(string));
            table.Columns.Add("Pc", typeof(int));
            table.Columns.Add("Weight", typeof(decimal));
            table.Columns.Add("Less", typeof(decimal));
            table.Columns.Add("NetWt", typeof(decimal));
            table.Columns.Add("Tunch", typeof(decimal));
            table.Columns.Add("Wastage", typeof(decimal));
            table.Columns.Add("TW", typeof(decimal));
            table.Columns.Add("Rate", typeof(decimal));
            table.Columns.Add("Fine", typeof(decimal));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("LastUpdated", typeof(DateTime));

            foreach (var stock in stocks)
            {
                table.Rows.Add(
                    stock.BillNo,
                    stock.Date,
                    stock.Item?.Name ?? "",
                    stock.Pc.HasValue ? stock.Pc : DBNull.Value,
                    stock.Weight.HasValue ? stock.Weight : DBNull.Value,
                    stock.Less.HasValue ? stock.Less : DBNull.Value,
                    stock.NetWt.HasValue ? stock.NetWt : DBNull.Value,
                    stock.Tunch.HasValue ? stock.Tunch : DBNull.Value,
                    stock.Wastage.HasValue ? stock.Wastage : DBNull.Value,
                    stock.TW.HasValue ? stock.TW : DBNull.Value,
                    stock.Rate.HasValue ? stock.Rate : DBNull.Value,
                    stock.Fine.HasValue ? stock.Fine : DBNull.Value,
                    stock.Amount.HasValue ? stock.Amount : DBNull.Value,
                    stock.LastUpdated.HasValue ? stock.LastUpdated : DBNull.Value
                );
            }

            return View(table);
        }

        [HttpGet]
        public IActionResult ExportToExcel()
        {
            var stocks = _context.OpeningStocks.Include(os => os.Item).ToList();

            decimal totalWeight = stocks.Sum(os => os.Weight ?? 0);
            decimal totalLess = stocks.Sum(os => os.Less ?? 0);
            decimal totalNetWt = stocks.Sum(os => os.NetWt ?? 0);
            decimal totalFine = stocks.Sum(os => os.Fine ?? 0);
            decimal totalAmount = stocks.Sum(os => os.Amount ?? 0);

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("OpeningStock");
                worksheet.Cells["A1"].LoadFromCollection(stocks.Select(os => new
                {
                    os.BillNo,
                    os.Date,
                    ItemName = os.Item?.Name,
                    os.Weight,
                    os.Less,
                    os.NetWt,
                    os.Fine,
                    os.Amount,
                    os.LastUpdated
                }), true);

                int rowCount = stocks.Count + 2;
                worksheet.Cells[rowCount, 1].Value = "Total";
                worksheet.Cells[rowCount, 4].Value = totalWeight;
                worksheet.Cells[rowCount, 5].Value = totalLess;
                worksheet.Cells[rowCount, 6].Value = totalNetWt;
                worksheet.Cells[rowCount, 7].Value = totalFine;
                worksheet.Cells[rowCount, 8].Value = totalAmount;

                worksheet.Cells.AutoFitColumns();
                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OpeningStockReport.xlsx");
            }
        }

        [HttpGet]
        public IActionResult PrintStock()
        {
            var stocks = _context.OpeningStocks.Include(os => os.Item).ToList();
            DataTable table = new DataTable();
            table.Columns.Add("BillNo", typeof(string));
            table.Columns.Add("Date", typeof(DateTime));
            table.Columns.Add("ItemName", typeof(string));

            foreach (var stock in stocks)
            {
                table.Rows.Add(stock.BillNo, stock.Date, stock.Item?.Name);
            }
            return View("PrintStock", table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOpeningStock([FromBody] DeleteStockRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.BillNo))
                {
                    return Json(new { success = false, error = "BillNo is required" });
                }

                var stocks = _context.OpeningStocks.Where(os => os.BillNo == request.BillNo);
                if (!stocks.Any())
                {
                    return Json(new { success = false, error = "No stock entry found with this BillNo" });
                }

                _context.OpeningStocks.RemoveRange(stocks);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private void CalculateDerivedFields(OpeningStockModel model)
        {
            model.NetWt = (model.Weight ?? 0) - (model.Less ?? 0);
            model.TW = (model.Tunch ?? 0) + (model.Wastage ?? 0);
            model.Fine = (model.NetWt ?? 0) * ((model.TW ?? 0) / 100);
            model.Amount = (model.Fine ?? 0) * (model.Rate ?? 0);
        }
    }

    public class DeleteStockRequest
    {
        public string? BillNo { get; set; }
    }
}