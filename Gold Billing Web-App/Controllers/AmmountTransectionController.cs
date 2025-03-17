using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace Gold_Billing_Web_App.Controllers
{
    public class AmmountTransectionController : Controller
    {
        private readonly IConfiguration configuration;

        public AmmountTransectionController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }

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
                    GroupName = row["GroupName"] != DBNull.Value ? row.Field<string>("GroupName")! : ""
                }).ToList();
            }
        }

        private List<PaymentModeDropDownModel> SetPaymentModeDropDown()
        {
            return new List<PaymentModeDropDownModel>
            {
                new PaymentModeDropDownModel { Id = 1, ModeName = "Cash" },
                new PaymentModeDropDownModel { Id = 2, ModeName = "Cheque" },
                new PaymentModeDropDownModel { Id = 3, ModeName = "Card" }
            };
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string type)
        {
            if (!new[] { "Payment", "Receive" }.Contains(type))
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

        private string GenerateSequentialBillNo(string transactionType)
        {
            string prefix = transactionType == "Payment" ? "PAYM" : "RECV";
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            int lastNumber = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand($"SELECT MAX(CAST(SUBSTRING(BillNo, 5, LEN(BillNo)) AS INT)) FROM AmountTransactions WHERE BillNo LIKE '{prefix}%'", connection);
                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    lastNumber = Convert.ToInt32(result);
                }
                lastNumber += 1;
                string billNo = $"{prefix}{lastNumber:D4}";
                if (billNo.Length > 20) throw new Exception("BillNo exceeds NVARCHAR(20) length.");
                return billNo;
            }
        }

        public IActionResult GenrateAmmountTransectionVoucher(string type = "Payment", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Payment", "Receive" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.PaymentModeDropDown = SetPaymentModeDropDown();
            var model = new AmountTransactionModel();

            if (!string.IsNullOrEmpty(billNo))
            {
                string? connectionString = configuration.GetConnectionString("ConnectionString");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("SP_AmountTransaction_SelectByBillNo", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BillNo", billNo);

                    SqlDataReader reader = command.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);

                    if (table.Rows.Count == 0)
                    {
                        return NotFound();
                    }

                    var row = table.Rows[0];
                    model.Id = row.Field<int>("Id");
                    model.BillNo = row.Field<string>("BillNo")!;
                    model.Date = row.Field<DateTime>("Date");
                    model.AccountId = row.Field<int>("AccountId");
                    model.Type = row.Field<string>("Type")!;
                    model.PaymentModeId = row.Field<int>("PaymentModeId");
                    model.Amount = row.Field<decimal>("Amount");
                    model.Narration = row["Narration"] != DBNull.Value ? row.Field<string>("Narration") : null;

                    ViewBag.SelectedAccountId = model.AccountId;
                }
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                model.Date = DateTime.Now;
                model.Type = type;
                model.Amount = 0;
                model.PaymentModeId = 0;

                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.AccountId = accountId.Value;
                }
            }

            return View(model);
        }

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
        public IActionResult AddAmountTransaction(AmountTransactionModel model, int? SelectedAccountId)
        {
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.PaymentModeDropDown = SetPaymentModeDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            if (string.IsNullOrEmpty(model.Type))
            {
                model.Type = Request.Form["Type"].FirstOrDefault() ?? "Payment";
            }

            if (!SelectedAccountId.HasValue)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
            }

            if (string.IsNullOrEmpty(model.BillNo) || model.BillNo.Length > 20)
            {
                ModelState.AddModelError("BillNo", "Bill Number is required and must not exceed 20 characters.");
            }

            if (string.IsNullOrEmpty(model.Type) || model.Type.Length > 10)
            {
                ModelState.AddModelError("Type", "Type is required and must not exceed 10 characters.");
            }

            if (model.PaymentModeId <= 0)
            {
                ModelState.AddModelError("PaymentModeId", "Payment Mode is required.");
            }

            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Amount must be greater than 0.");
            }

            if (model.Narration != null && model.Narration.Length > 500)
            {
                ModelState.AddModelError("Narration", "Narration must not exceed 500 characters.");
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

                    model.AccountId = SelectedAccountId!.Value;
                    SqlCommand command = model.Id > 0 ? new SqlCommand("SP_AmountTransaction_Update", connection) : new SqlCommand("SP_AmountTransaction_Insert", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    if (model.Id > 0) command.Parameters.AddWithValue("@Id", model.Id);
                    command.Parameters.AddWithValue("@BillNo", model.BillNo);
                    command.Parameters.AddWithValue("@Date", model.Date);
                    command.Parameters.AddWithValue("@AccountId", model.AccountId);
                    command.Parameters.AddWithValue("@Type", model.Type);
                    command.Parameters.AddWithValue("@PaymentModeId", model.PaymentModeId);
                    command.Parameters.AddWithValue("@Amount", model.Amount);
                    command.Parameters.AddWithValue("@Narration", (object)model.Narration! ?? DBNull.Value);
                    command.ExecuteNonQuery();

                    var account = SetAccountDropDown().FirstOrDefault(a => a.Id == model.AccountId);
                    if (account != null)
                    {
                        decimal amountAdjustment = 0;
                        if (account.GroupName == "Supplier")
                        {
                            amountAdjustment = model.Type == "Payment" ? -model.Amount : model.Amount;
                        }
                        else if (account.GroupName == "Customer")
                        {
                            amountAdjustment = model.Type == "Receive" ? -model.Amount : model.Amount;
                        }

                        SqlCommand updateCommand = new SqlCommand("UPDATE Account SET Amount = ISNULL(Amount, 0) + @AmountAdjustment WHERE AccountId = @AccountId", connection);
                        updateCommand.Parameters.AddWithValue("@AmountAdjustment", amountAdjustment);
                        updateCommand.Parameters.AddWithValue("@AccountId", model.AccountId);
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