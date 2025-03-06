using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Gold_Billing_Web_App.Controllers
{
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
                    ItemName = row.Field<string>("Name")!,
                    GroupName = row.Field<string>("GroupName") ?? "" // Handle NULL GroupName
                }).ToList();
            }
        }

        // Add/Edit Transaction (GET)
        public IActionResult AddTransaction(string type, int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Purchase", "Sale", "PurchaseReturn", "SaleReturn" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            var model = new TransactionViewModel();

            if (!string.IsNullOrEmpty(billNo))
            {
                string? connectionString = configuration.GetConnectionString("ConnectionString");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("SP_Transaction_SelectByBillNo", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BillNo", billNo);

                    SqlDataReader reader = command.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);

                    if (table.Rows.Count == 0)
                    {
                        return NotFound();
                    }

                    var firstRow = table.Rows[0];
                    model.BillNo = billNo;
                    model.Date = firstRow.Field<DateTime>("Date");
                    model.Narration = firstRow["Narration"] != DBNull.Value ? firstRow["Narration"] as string : null;
                    model.TransactionType = type;
                    model.Items = table.AsEnumerable().Select(row => new TransactionModel
                    {
                        Id = row.Field<int>("Id"),
                        TransactionType = row.Field<string>("TransactionType")!,
                        AccountId = row["AccountId"] != DBNull.Value ? row.Field<int>("AccountId") : null,
                        ItemId = row.Field<int>("ItemId"),
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

                    if (model.Items.Any() && model.Items[0].AccountId.HasValue)
                    {
                        ViewBag.SelectedAccountId = model.Items[0].AccountId;
                    }
                }
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                model.Date = DateTime.Now;
                model.TransactionType = type;
                model.Items = new List<TransactionModel> { new TransactionModel { TransactionType = type } };

                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.Items[0].AccountId = accountId;
                }
            }

            return View(model);
        }

        // Get Previous Balance
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
        public IActionResult AddTransaction(TransactionViewModel model, int? SelectedAccountId)
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            if (string.IsNullOrEmpty(model.TransactionType))
            {
                model.TransactionType = Request.Form["TransactionType"].FirstOrDefault() ?? "Purchase";
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
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            string? connectionString = configuration.GetConnectionString("ConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    foreach (var item in model.Items)
                    {
                        item.AccountId = SelectedAccountId;
                        item.TransactionType = model.TransactionType;

                        // Debug: Log incoming data
                        Console.WriteLine($"Item {item.ItemId}: Client Amount = {item.Amount}, Pc = {item.Pc}, Rate = {item.Rate}, Weight = {item.Weight}");

                        // Only recalculate if Amount is null
                        string groupName = itemGroups[item.ItemId.Value];
                        if (item.Amount == null)
                        {
                            CalculateDerivedFields(item, groupName);
                        }

                        Console.WriteLine($"Item {item.ItemId}: Final Amount = {item.Amount}");

                        SqlCommand command = item.Id > 0 ? new SqlCommand("SP_Transaction_Update", connection) : new SqlCommand("SP_Transaction_Insert", connection);
                        command.CommandType = CommandType.StoredProcedure;
                        if (item.Id > 0) command.Parameters.AddWithValue("@Id", item.Id);
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
                        command.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                }

                string redirectUrl = Url.Action("ViewStock", "OpeningStock");
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }

        // Calculate Derived Fields with group-specific logic
        private void CalculateDerivedFields(TransactionModel model, string groupName)
        {
            if (groupName == "PC Gold Jewelry")
            {
                model.NetWt = 0;
                model.TW = 0;
                model.Fine = 0;
                model.Amount = (model.Pc ?? 0) * (model.Rate ?? 0);
            }
            else // Gold Jewelry or PC/Weight Jewelry
            {
                model.NetWt = (model.Weight ?? 0) - (model.Less ?? 0);
                model.TW = (model.Tunch ?? 0) + (model.Wastage ?? 0);
                model.Fine = (model.NetWt ?? 0) * (model.TW ?? 0) / 100;
                model.Amount = (model.Fine ?? 0) * (model.Rate ?? 0);
            }
        }
    }
}