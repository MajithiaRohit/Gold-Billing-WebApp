using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

public class TransactionController : Controller
{
    private readonly IConfiguration configuration;

    public TransactionController(IConfiguration _configuration)
    {
        configuration = _configuration;
    }

    // Fetch Account Dropdown
    public List<AccountDropDownModel> SetAccountDropDown()
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand("SP_AccountDropDown", connection);
            command.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = command.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(reader);

            return dataTable.AsEnumerable().Select(row => new AccountDropDownModel
            {
                Id = row.Field<int>("Id"),
                AccountName = row.Field<string>("Name")!
            }).ToList();
        }
    }

    // Generate Sequential Bill Number
    private string GenerateSequentialBillNo(string transactionType)
    {
        string prefix = transactionType.Substring(0, 4).ToUpper();
        string? connectionString = configuration.GetConnectionString("ConnectionString");
        int lastNumber = 0;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand($"SELECT MAX(CAST(SUBSTRING(BillNo, 5, LEN(BillNo)) AS INT)) FROM Transactions WHERE BillNo LIKE '{prefix}%'", connection);
            object result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                lastNumber = Convert.ToInt32(result);
            }
            lastNumber += 1;
            return $"{prefix}{lastNumber:D4}";
        }
    }

    // Fetch Item Dropdown
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

    // Add Transaction (GET)
    public IActionResult AddTransaction(string type, int? accountId = null)
    {
        if (!new[] { "Purchase", "Sale", "PurchaseReturn", "SaleReturn" }.Contains(type))
        {
            return NotFound();
        }

        ViewBag.ItemDropDown = SetItemDropDown();
        ViewBag.AccountDropDown = SetAccountDropDown();
        var model = new TransactionViewModel
        {
            BillNo = GenerateSequentialBillNo(type),
            Date = DateTime.Now,
            TransactionType = type,
            Items = new List<TransactionModel> { new TransactionModel { TransactionType = type } }
        };

        if (accountId.HasValue)
        {
            ViewBag.SelectedAccountId = accountId;
        }

        return View(model);
    }

    // Get Previous Balance (Using your SP_GetPreviousBalance)
    [HttpGet]
    public IActionResult GetPreviousBalance(int accountId)
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand("SP_GetPreviousBalance", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@AccountId", accountId);
            SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return Json(new
                {
                    fine = reader.GetDecimal("Fine"),
                    amount = reader.GetDecimal("Amount")
                });
            }
        }
        return Json(new { fine = 0.0, amount = 0.0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddTransaction(TransactionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            return View(model);
        }

        string? connectionString = configuration.GetConnectionString("ConnectionString");
        int? selectedAccountId = null;

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                foreach (var item in model.Items)
                {
                    CalculateDerivedFields(item);
                    SqlCommand command = new SqlCommand("SP_Transaction_Insert", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@TransactionType", item.TransactionType);
                    command.Parameters.AddWithValue("@BillNo", model.BillNo);
                    command.Parameters.AddWithValue("@Date", model.Date);
                    command.Parameters.AddWithValue("@AccountId", item.AccountId ?? (object)DBNull.Value);
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

                    if (!selectedAccountId.HasValue && item.AccountId.HasValue)
                    {
                        selectedAccountId = item.AccountId;
                    }
                }
            }

            string? redirectUrl = model.Items.Any(i => i.TransactionType == "Purchase") && selectedAccountId.HasValue
                ? Url.Action("AddTransaction", new { type = "Sale", accountId = selectedAccountId })
                : Url.Action("ViewStock", "OpeningStock");

            return Json(new { success = true, redirectUrl });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"An error occurred while saving: {ex.Message}" });
        }
    }

    // Calculate Derived Fields
    private void CalculateDerivedFields(TransactionModel model)
    {
        model.NetWt = (model.Weight ?? 0) - (model.Less ?? 0);
        model.TW = (model.Tunch ?? 0) + (model.Wastage ?? 0);
        model.Fine = (model.NetWt ?? 0) * ((model.TW ?? 0) / 100);
        model.Amount = (model.Fine ?? 0) * (model.Rate ?? 0);
    }
}