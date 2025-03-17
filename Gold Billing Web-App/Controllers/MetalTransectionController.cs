using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Gold_Billing_Web_App.Controllers
{
    public class MetalTransectionController : Controller
    {
        private readonly IConfiguration configuration;

        public MetalTransectionController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }

        // Helper method to populate Account dropdown
        private List<AccountDropDownModel> SetAccountDropDown()
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
                    AccountName = row.Field<string>("Name")!,
                    GroupName = row["GroupName"] != DBNull.Value ? row.Field<string>("GroupName") : ""
                }).ToList();
            }
        }

        // Helper method to populate Item dropdown
        // Helper method to populate Item dropdown with specific items
        private List<ItemDropDownModel> SetItemDropDown()
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

                // Filter items to only include "Fine metal", "Cadbury", and "Dhal"
                var allowedItems = new List<string> { "Fine Metal", "Cadbury", "Dhal" };
                return dataTable.AsEnumerable()
                    .Where(row => allowedItems.Contains(row.Field<string>("Name")!))
                    .Select(row => new ItemDropDownModel
                    {
                        Id = row.Field<int>("Id"),
                        ItemName = row.Field<string>("Name")!
                    }).ToList();
            }
        }

        // Add this new action
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
                return Json(new { success = false, error = $"Error generating bill number: {ex.Message}" });
            }
        }

        // Generate a sequential bill number
        private string GenerateSequentialBillNo(string transactionType)
        {
            string prefix = transactionType == "Payment" ? "PAYM" : "RECV";
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            int lastNumber = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand($"SELECT MAX(CAST(SUBSTRING(BillNo, 5, LEN(BillNo)) AS INT)) FROM MetalTransactions WHERE BillNo LIKE '{prefix}%'", connection);
                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    lastNumber = Convert.ToInt32(result);
                }
                lastNumber += 1;
                return $"{prefix}{lastNumber:D4}";
            }
        }

        // Action to generate the metal transaction voucher
        public IActionResult GenrateMetalTransectionVoucher(string type = "Payment", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Payment", "Receipt" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new MetalTransactionViewModel();

            if (!string.IsNullOrEmpty(billNo))
            {
                string? connectionString = configuration.GetConnectionString("ConnectionString");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("SP_MetalTransaction_SelectByBillNo", connection);
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
                    model.Type = type;
                    model.Items = table.AsEnumerable().Select(row => new MetalTransactionModel
                    {
                        Id = row.Field<int>("Id"),
                        Type = row.Field<string>("Type")!,
                        AccountId = row["AccountId"] != DBNull.Value ? row.Field<int>("AccountId") : null,
                        ItemId = row.Field<int>("ItemId"),
                        GrossWeight = row["GrossWeight"] != DBNull.Value ? row.Field<decimal>("GrossWeight") : null,
                        Tunch = row["Tunch"] != DBNull.Value ? row.Field<decimal>("Tunch") : null,
                        Fine = row["Fine"] != DBNull.Value ? row.Field<decimal>("Fine") : null
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
                model.Type = type;
                model.Items = new List<MetalTransactionModel> { new MetalTransactionModel { Type = type } };

                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.Items[0].AccountId = accountId;
                }
            }

            return View(model);
        }

        // Action to get the previous balance of an account
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

        // Action to add a metal transaction
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddMetalTransaction(MetalTransactionViewModel model, int? SelectedAccountId)
        {
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            if (string.IsNullOrEmpty(model.Type))
            {
                model.Type = Request.Form["Type"].FirstOrDefault() ?? "Payment";
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
                    }
                    if (!item.GrossWeight.HasValue || item.GrossWeight <= 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].GrossWeight", "Gross Weight is required.");
                    }
                    if (!item.Tunch.HasValue || item.Tunch <= 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
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
                    decimal totalFine = 0;

                    // Save MetalTransactions and calculate total fine
                    foreach (var item in model.Items!)
                    {
                        item.AccountId = SelectedAccountId;
                        item.Type = model.Type;
                        item.Fine = (item.GrossWeight ?? 0) * (item.Tunch ?? 0) / 100;
                        totalFine += item.Fine ?? 0; // Aggregate total fine for this transaction

                        SqlCommand command = item.Id.HasValue && item.Id > 0 ? new SqlCommand("SP_MetalTransaction_Update", connection) : new SqlCommand("SP_MetalTransaction_Insert", connection);
                        command.CommandType = CommandType.StoredProcedure;
                        if (item.Id.HasValue) command.Parameters.AddWithValue("@Id", item.Id);
                        command.Parameters.AddWithValue("@Type", item.Type);
                        command.Parameters.AddWithValue("@BillNo", model.BillNo);
                        command.Parameters.AddWithValue("@Date", model.Date);
                        command.Parameters.AddWithValue("@AccountId", item.AccountId ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ItemId", item.ItemId);
                        command.Parameters.AddWithValue("@GrossWeight", item.GrossWeight ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Tunch", item.Tunch ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Fine", item.Fine ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);
                        command.ExecuteNonQuery();
                    }

                    // Update Account balance based on GroupName and Transaction Type
                    var account = SetAccountDropDown().FirstOrDefault(a => a.Id == SelectedAccountId);
                    if (account != null)
                    {
                        decimal fineAdjustment = 0;
                        if (account.GroupName == "Supplier")
                        {
                            fineAdjustment = model.Type == "Payment" ? -totalFine : totalFine;
                        }
                        else if (account.GroupName == "Customer")
                        {
                            fineAdjustment = model.Type == "Receipt" ? -totalFine : totalFine;
                        }

                        // Update the Account table with the new balance
                        SqlCommand updateCommand = new SqlCommand("UPDATE Account SET Fine = ISNULL(Fine, 0) + @FineAdjustment WHERE AccountId = @AccountId", connection);
                        updateCommand.Parameters.AddWithValue("@FineAdjustment", fineAdjustment);
                        updateCommand.Parameters.AddWithValue("@AccountId", SelectedAccountId);
                        updateCommand.ExecuteNonQuery();
                    }
                }

                string redirectUrl = Url.Action("Index", "Home")!;
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }
    }
}