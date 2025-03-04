using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Data;

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

        // Set ViewBag properties
        ViewBag.Action = "AddOpeningStock"; // Form action
        ViewBag.Title = "Add Opening Stock"; // Page title

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddOpeningStock(OpeningStockViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "AddOpeningStock"; // Re-set for invalid state
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
            ViewBag.Action = "AddOpeningStock"; // Re-set for error state
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

        // Set ViewBag properties
        ViewBag.Action = "AddNewStock"; // Form action
        ViewBag.Title = "Add New Stock"; // Page title

        return View("AddOpeningStock", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddNewStock(OpeningStockViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "AddNewStock"; // Re-set for invalid state
            ViewBag.Title = "Add New Stock";
            return View("AddOpeningStock", model);
        }

        return AddOpeningStock(model); // Reuse the same logic
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
        // Set ViewBag properties
        ViewBag.Action = "EditOpeningStock"; // Form action
        ViewBag.Title = "Edit Stock"; // Page title (since Items have Id > 0)

        return View("AddOpeningStock", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditOpeningStock(OpeningStockViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "EditOpeningStock"; // Re-set for invalid state
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
            ViewBag.Action = "EditOpeningStock"; // Re-set for error state
            ViewBag.Title = "Edit Stock";
            return View("AddOpeningStock", model);
        }
    }

    public IActionResult ViewStock()
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand("SP_Stock_SelectAll", connection);
            command.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
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
            SqlCommand command = new SqlCommand("SP_Stock_SelectAll", connection);
            command.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Stock");
                worksheet.Cells["A1"].LoadFromDataTable(table, true);
                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StockReport.xlsx");
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
            SqlCommand command = new SqlCommand("SP_Stock_SelectAll", connection);
            command.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return View("PrintStock", table); // Render a new PrintStock.cshtml view
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