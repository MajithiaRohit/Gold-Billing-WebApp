using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Data;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        private readonly IConfiguration configuration;

        public OpeningStockController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }

        private string GenerateSequentialBillNo(string prefix = "BILL")
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            int lastNumber = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand($"SELECT MAX(CAST(SUBSTRING(BillNo, {prefix.Length + 1}, LEN(BillNo)) AS INT)) FROM OpeningStock WHERE BillNo LIKE '{prefix}%'", connection);
                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    lastNumber = Convert.ToInt32(result);
                }
                lastNumber += 1;
                return $"{prefix}{lastNumber:D4}";
            }
        }

        private bool IsOpeningStockAdded()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SP_CheckOpeningStockStatus", connection);
                command.CommandType = CommandType.StoredProcedure;
                object result = command.ExecuteScalar();
                return result != null && Convert.ToBoolean(result);
            }
        }

        public List<ItemDropDownModel> SetItemDropDown()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SP_ItemDropDown", connection);
                command.CommandType = CommandType.StoredProcedure;
                SqlDataReader reader = command.ExecuteReader();
                DataTable dataTable = new DataTable();
                dataTable.Load(reader);
                return dataTable.AsEnumerable().Select(row => new ItemDropDownModel
                {
                    Id = row.Field<int>("Id"),
                    ItemName = row.Field<string>("Name")!
                }).ToList();
            }
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
        public IActionResult AddOpeningStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "AddOpeningStock";
                ViewBag.Title = "Add Opening Stock";
                return View(model);
            }
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    foreach (var item in model.Items)
                    {
                        CalculateDerivedFields(item);
                        SqlCommand command = new SqlCommand("SP_OpeningStock_Insert", connection);
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@BillNo", model.BillNo);
                        command.Parameters.AddWithValue("@Date", model.Date);
                        command.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ItemId", item.ItemId);
                        command.Parameters.AddWithValue("@Pc", item.Pc ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Weight", item.Weight ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Less", item.Less ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@NetWt", item.NetWt ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Tunch", item.Tunch ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Wastage", item.Wastage ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@TW", item.TW ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Rate", item.Rate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Fine", item.Fine ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Amount", item.Amount ?? (object)DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                }
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
        public IActionResult AddNewStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "AddNewStock";
                ViewBag.Title = "Add New Stock";
                return View("AddOpeningStock", model);
            }
            return AddOpeningStock(model);
        }

        public IActionResult EditOpeningStock(string billNo)
        {
            if (string.IsNullOrEmpty(billNo))
            {
                return NotFound();
            }
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            var model = new OpeningStockViewModel();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SP_OpeningStock_SelectByBillNo", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@BillNo", billNo);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);
                if (table.Rows.Count == 0)
                {
                    return NotFound();
                }
                model.BillNo = billNo;
                model.Date = table.Rows[0].Field<DateTime>("Date");
                model.Narration = table.Rows[0]["Narration"] as string;
                model.Items = table.AsEnumerable().Select(row => new OpeningStockModel
                {
                    Id = row.Field<int>("Id"),
                    ItemId = row.Field<int>("ItemId"),
                    ItemName = row.Field<string>("ItemName") ?? "",
                    Pc = row["Pc"] != DBNull.Value ? row.Field<int>("Pc") : null,
                    Weight = row["Weight"] != DBNull.Value ? row.Field<decimal>("Weight") : null,
                    Less = row["Less"] != DBNull.Value ? row.Field<decimal>("Less") : null,
                    NetWt = row["NetWt"] != DBNull.Value ? row.Field<decimal>("NetWt") : null,
                    Tunch = row["Tunch"] != DBNull.Value ? row.Field<decimal>("Tunch") : null,
                    Wastage = row["Wastage"] != DBNull.Value ? row.Field<decimal>("Wastage") : null,
                    TW = row["TW"] != DBNull.Value ? row.Field<decimal>("TW") : null,
                    Rate = row["Rate"] != DBNull.Value ? row.Field<decimal>("Rate") : null,
                    Fine = row["Fine"] != DBNull.Value ? row.Field<decimal>("Fine") : null,
                    Amount = row["Amount"] != DBNull.Value ? row.Field<decimal>("Amount") : null
                }).ToList();
            }
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "EditOpeningStock";
            ViewBag.Title = "Edit Stock";
            return View("AddOpeningStock", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditOpeningStock(OpeningStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ItemDropDown = SetItemDropDown();
                ViewBag.Action = "EditOpeningStock";
                ViewBag.Title = "Edit Stock";
                return View("AddOpeningStock", model);
            }
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand deleteCommand = new SqlCommand("DELETE FROM OpeningStock WHERE BillNo = @BillNo", connection);
                    deleteCommand.Parameters.AddWithValue("@BillNo", model.BillNo);
                    deleteCommand.ExecuteNonQuery();
                    foreach (var item in model.Items)
                    {
                        CalculateDerivedFields(item);
                        SqlCommand command = new SqlCommand("SP_OpeningStock_Insert", connection);
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@BillNo", model.BillNo);
                        command.Parameters.AddWithValue("@Date", model.Date);
                        command.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ItemId", item.ItemId);
                        command.Parameters.AddWithValue("@Pc", item.Pc ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Weight", item.Weight ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Less", item.Less ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@NetWt", item.NetWt ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Tunch", item.Tunch ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Wastage", item.Wastage ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@TW", item.TW ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Rate", item.Rate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Fine", item.Fine ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Amount", item.Amount ?? (object)DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                }
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
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            const int pageSize = 10;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand countCommand = new SqlCommand("SELECT COUNT(*) FROM (SELECT DISTINCT BillNo FROM OpeningStock) AS UniqueBills", connection);
                int totalRecords = (int)countCommand.ExecuteScalar();
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages));
                SqlCommand command = new SqlCommand("SP_Stock_SelectAll_Paginated", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@PageNumber", page);
                command.Parameters.AddWithValue("@PageSize", pageSize);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                return View(table);
            }
        }

        [HttpGet]
        public IActionResult ExportToExcel()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SP_OpeningStock_SelectAll", connection);
                command.CommandType = CommandType.StoredProcedure;
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                // Calculate totals with nullable handling
                decimal totalWeight = table.AsEnumerable().Sum(row => row["Weight"] is DBNull ? 0 : row.Field<decimal>("Weight"));
                decimal totalLess = table.AsEnumerable().Sum(row => row["Less"] is DBNull ? 0 : row.Field<decimal>("Less"));
                decimal totalNetWt = table.AsEnumerable().Sum(row => row["NetWt"] is DBNull ? 0 : row.Field<decimal>("NetWt"));
                decimal totalFine = table.AsEnumerable().Sum(row => row["Fine"] is DBNull ? 0 : row.Field<decimal>("Fine"));
                decimal totalAmount = table.AsEnumerable().Sum(row => row["Amount"] is DBNull ? 0 : row.Field<decimal>("Amount"));

                // Add totals row
                DataRow totalRow = table.NewRow();
                totalRow["BillNo"] = "Total";
                totalRow["ItemName"] = "";
                totalRow["Weight"] = totalWeight;
                totalRow["Less"] = totalLess;
                totalRow["NetWt"] = totalNetWt;
                totalRow["Fine"] = totalFine;
                totalRow["Amount"] = totalAmount;
                totalRow["LastUpdated"] = DBNull.Value;
                table.Rows.Add(totalRow);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("OpeningStock");
                    worksheet.Cells["A1"].LoadFromDataTable(table, true);
                    worksheet.Cells.AutoFitColumns();
                    var stream = new MemoryStream(package.GetAsByteArray());
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OpeningStockReport.xlsx");
                }
            }
        }

        [HttpGet]
        public IActionResult PrintStock()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SP_OpeningStock_SelectAll", connection);
                command.CommandType = CommandType.StoredProcedure;
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);
                return View("PrintStock", table);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteOpeningStock([FromBody] DeleteStockRequest request)
        {
            try
            {
                Console.WriteLine("DeleteOpeningStock action invoked");
                string? billNo = request?.BillNo; // Nullable string
                Console.WriteLine($"Received BillNo from request: '{billNo}'");

                if (string.IsNullOrEmpty(billNo))
                {
                    Console.WriteLine("BillNo is null or empty");
                    return Json(new { success = false, error = "BillNo is required" });
                }

                string? connectionString = configuration.GetConnectionString("ConnectionString");
                Console.WriteLine($"Connection string: '{connectionString}'");
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("Connection string is null");
                    return Json(new { success = false, error = "Database connection string is missing" });
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    Console.WriteLine("Database connection opened successfully");
                    SqlCommand command = new SqlCommand("SP_OpeningStock_Delete", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BillNo", billNo);
                    Console.WriteLine($"Executing SP_OpeningStock_Delete with BillNo: '{billNo}'");
                    int rowsAffected = command.ExecuteNonQuery();
                    Console.WriteLine($"Rows affected: {rowsAffected}");
                    if (rowsAffected == 0)
                    {
                        Console.WriteLine("No rows deleted - BillNo not found in database");
                        return Json(new { success = false, error = "No stock entry found with this BillNo" });
                    }
                }
                Console.WriteLine("Deletion successful");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
        public string? BillNo { get; set; } // Made nullable to avoid null reference warnings
    }
}