using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using OfficeOpenXml;
using Gold_Billing_Web_App.Models.ViewModels;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        private readonly AppDbContext _context;

        public OpeningStockController(AppDbContext context)
        {
            _context = context;
        }

        private async Task<int> GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                Console.WriteLine("Error: User not logged in or user ID not found.");
                throw new InvalidOperationException("User not logged in.");
            }

            if (!int.TryParse(userIdString, out int userId))
            {
                Console.WriteLine($"Error: UserId '{userIdString}' is not a valid integer.");
                throw new InvalidOperationException($"UserId '{userIdString}' is not a valid integer.");
            }

            return await Task.FromResult(userId);
        }

        private async Task<string> GenerateSequentialBillNo(string prefix = "BILL")
        {
            var userId = await GetCurrentUserId();
            var lastBill = await _context.OpeningStocks
                .Where(os => os.BillNo.StartsWith(prefix) && os.UserId == userId)
                .OrderByDescending(os => os.BillNo)
                .Select(os => os.BillNo)
                .FirstOrDefaultAsync();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastBill))
            {
                lastNumber = int.Parse(lastBill.Substring(prefix.Length));
            }
            return $"{prefix}{lastNumber + 1:D4}"; // Fixed formatting
        }

        private async Task<bool> IsOpeningStockAdded()
        {
            var userId = await GetCurrentUserId();
            return await _context.OpeningStocks.AnyAsync(os => os.UserId == userId);
        }

        private List<ItemDropDownModel> SetItemDropDown()
        {
            var items = _context.Items
                .Select(i => new ItemDropDownModel
                {
                    Id = i.Id!.Value,
                    ItemName = i.Name!
                })
                .ToList();
            Console.WriteLine($"Fetched {items.Count} items for dropdown.");
            return items;
        }

        public async Task<IActionResult> AddOpeningStock()
        {
            if (await IsOpeningStockAdded())
            {
                Console.WriteLine("Opening stock already added for user. Redirecting to ViewStock.");
                TempData["Message"] = "Opening stock has already been added. Use 'New Stock' to add more.";
                return RedirectToAction("ViewStock");
            }
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new OpeningStockViewModel
            {
                BillNo = await GenerateSequentialBillNo(),
                Date = DateTime.Now,
                UserId = await GetCurrentUserId(),
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
            model.BillNo = string.IsNullOrEmpty(model.BillNo) ? await GenerateSequentialBillNo() : model.BillNo;
            model.UserId = await GetCurrentUserId(); // UserId is now int

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"Validation failed for AddOpeningStock: {string.Join(", ", errors)}");
                ViewBag.ItemDropDown = SetItemDropDown();
                return View(model);
            }

            try
            {
                var userId = await GetCurrentUserId();
                Console.WriteLine($"Adding opening stock for BillNo: {model.BillNo}, UserId: {userId}");
                foreach (var item in model.Items)
                {
                    CalculateDerivedFields(item);
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.LastUpdated = DateTime.Now;
                    item.UserId = userId; // Now both are int
                    _context.OpeningStocks.Add(item);
                    Console.WriteLine($"Added item to context: ItemId={item.ItemId}, Weight={item.Weight}, Fine={item.Fine}");
                }
                Console.WriteLine($"Saving changes to database for BillNo: {model.BillNo}");
                await _context.SaveChangesAsync();
                Console.WriteLine($"Successfully saved opening stock for BillNo: {model.BillNo}");
                TempData["SweetAlertMessage"] = $"Opening stock for BillNo {model.BillNo} added successfully!";
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while saving opening stock for BillNo: {model.BillNo}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.ItemDropDown = SetItemDropDown();
                return View(model);
            }
        }

        public async Task<IActionResult> AddNewStock()
        {
            var items = SetItemDropDown();
            if (!items.Any())
            {
                Console.WriteLine("No items available in the Item table for AddNewStock.");
                TempData["Error"] = "No items available. Please add items before adding new stock.";
                return RedirectToAction("ViewStock");
            }
            ViewBag.ItemDropDown = items;
            var model = new OpeningStockViewModel
            {
                BillNo = await GenerateSequentialBillNo("NEW"),
                Date = DateTime.Now,
                UserId = await GetCurrentUserId(),
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
            if (string.IsNullOrEmpty(model.BillNo) || !model.BillNo.StartsWith("NEW"))
            {
                model.BillNo = await GenerateSequentialBillNo("NEW");
            }
            model.UserId = await GetCurrentUserId(); // UserId is now int

            var existingBill = await _context.OpeningStocks.AnyAsync(os => os.BillNo == model.BillNo);
            if (existingBill)
            {
                Console.WriteLine($"Duplicate BillNo detected: {model.BillNo}. Generating a new one.");
                model.BillNo = await GenerateSequentialBillNo("NEW");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"Validation failed for AddNewStock: {string.Join(", ", errors)}");
                ViewBag.ItemDropDown = SetItemDropDown();
                return View("AddOpeningStock", model);
            }

            try
            {
                var userId = await GetCurrentUserId();
                Console.WriteLine($"Adding new stock for BillNo: {model.BillNo}, UserId: {userId}");
                foreach (var item in model.Items)
                {
                    CalculateDerivedFields(item);
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.LastUpdated = DateTime.Now;
                    item.UserId = userId; // Now both are int
                    _context.OpeningStocks.Add(item);
                    Console.WriteLine($"Added item to context: ItemId={item.ItemId}, Weight={item.Weight}, Fine={item.Fine}, UserId={item.UserId}");
                }
                Console.WriteLine($"Saving changes to database for BillNo: {model.BillNo}");
                await _context.SaveChangesAsync();
                Console.WriteLine($"Successfully saved new stock for BillNo: {model.BillNo}");
                TempData["SweetAlertMessage"] = $"Stock for BillNo {model.BillNo} added successfully!";
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while saving new stock for BillNo: {model.BillNo}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.ItemDropDown = SetItemDropDown();
                return View("AddOpeningStock", model);
            }
        }

        public async Task<IActionResult> EditOpeningStock(string billNo)
        {
            if (string.IsNullOrEmpty(billNo))
            {
                Console.WriteLine("BillNo is null or empty in EditOpeningStock.");
                return NotFound();
            }

            var userId = await GetCurrentUserId();
            var stocks = await _context.OpeningStocks
                .Where(os => os.BillNo == billNo && os.UserId == userId)
                .Include(os => os.Item)
                .ToListAsync();

            if (!stocks.Any())
            {
                Console.WriteLine($"No stock entries found for BillNo: {billNo}, UserId: {userId}");
                return NotFound();
            }

            var model = new OpeningStockViewModel
            {
                BillNo = billNo,
                Date = stocks.First().Date,
                Narration = stocks.First().Narration,
                UserId = userId,
                Items = stocks
            };

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "EditOpeningStock";
            ViewBag.Title = "Edit Stock";
            ViewBag.AllowNewStock = true; // Enable adding new stock in edit mode
            return View("AddOpeningStock", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOpeningStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"Validation failed for EditOpeningStock: {string.Join(", ", errors)}");
                ViewBag.ItemDropDown = SetItemDropDown();
                return View("AddOpeningStock", model);
            }

            try
            {
                var userId = await GetCurrentUserId();
                Console.WriteLine($"Editing stock for BillNo: {model.BillNo}, UserId: {userId}");

                // Fetch existing items
                var existingStocks = await _context.OpeningStocks
                    .Where(os => os.BillNo == model.BillNo && os.UserId == userId)
                    .ToListAsync();

                if (!existingStocks.Any())
                {
                    Console.WriteLine($"No stock entries found for BillNo: {model.BillNo}, UserId: {userId}");
                    return NotFound();
                }

                // Update existing items
                foreach (var item in model.Items)
                {
                    CalculateDerivedFields(item);
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.LastUpdated = DateTime.Now;
                    item.UserId = userId; // Now both are int

                    var existingItem = existingStocks.FirstOrDefault(os => os.Id == item.Id);
                    if (existingItem != null)
                    {
                        _context.Entry(existingItem).CurrentValues.SetValues(item);
                        Console.WriteLine($"Updated item in context: ItemId={item.ItemId}, Weight={item.Weight}, Fine={item.Fine}");
                    }
                    else
                    {
                        _context.OpeningStocks.Add(item);
                        Console.WriteLine($"Added new item to context: ItemId={item.ItemId}, Weight={item.Weight}, Fine={item.Fine}, UserId={item.UserId}");
                    }
                }

                Console.WriteLine($"Saving changes to database for BillNo: {model.BillNo}");
                await _context.SaveChangesAsync();
                Console.WriteLine($"Successfully edited stock for BillNo: {model.BillNo}");
                TempData["SweetAlertMessage"] = $"Stock for BillNo {model.BillNo} edited successfully!";
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while editing stock for BillNo: {model.BillNo}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.ItemDropDown = SetItemDropDown();
                return View("AddOpeningStock", model);
            }
        }

        public async Task<IActionResult> ViewStock(int page = 1, string sortBy = "BillNoAsc")
        {
            const int pageSize = 10;

            var userId = await GetCurrentUserId();
            var billNosQuery = _context.OpeningStocks
                .Where(os => os.UserId == userId)
                .Select(os => os.BillNo)
                .Distinct();

            if (sortBy == "BillNoDesc")
            {
                billNosQuery = billNosQuery.OrderByDescending(billNo => billNo);
            }
            else
            {
                billNosQuery = billNosQuery.OrderBy(billNo => billNo);
            }

            var totalRecords = await billNosQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            var paginatedBillNos = await billNosQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var stocks = await _context.OpeningStocks
                .Include(os => os.Item)
                .Where(os => paginatedBillNos.Contains(os.BillNo) && os.UserId == userId)
                .OrderBy(os => os.BillNo)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.SortBy = sortBy;

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
        public async Task<IActionResult> ExportToExcel()
        {
            var userId = await GetCurrentUserId();
            var stocks = await _context.OpeningStocks
                .Include(os => os.Item)
                .Where(os => os.UserId == userId)
                .ToListAsync();

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
        public async Task<IActionResult> PrintStock()
        {
            var userId = await GetCurrentUserId();
            var stocks = await _context.OpeningStocks
                .Include(os => os.Item)
                .Where(os => os.UserId == userId)
                .ToListAsync();

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
                    Console.WriteLine("DeleteOpeningStock failed: BillNo is null or empty.");
                    return Json(new { success = false, error = "BillNo is required" });
                }

                var userId = await GetCurrentUserId();
                var stocks = _context.OpeningStocks
                    .Where(os => os.BillNo == request.BillNo && os.UserId == userId);
                if (!stocks.Any())
                {
                    Console.WriteLine($"No stock entries found to delete for BillNo: {request.BillNo}, UserId: {userId}");
                    return Json(new { success = false, error = "No stock entry found with this BillNo for the current user" });
                }

                _context.OpeningStocks.RemoveRange(stocks);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Successfully deleted stock entries for BillNo: {request.BillNo}");
                return Json(new { success = true, message = $"Stock for BillNo {request.BillNo} deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while deleting stock for BillNo: {request?.BillNo}: {ex.Message}");
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