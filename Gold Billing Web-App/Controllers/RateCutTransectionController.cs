using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Gold_Billing_Web_App.Controllers
{
    public class RateCutTransectionController : Controller
    {
        private readonly IConfiguration configuration;

        public RateCutTransectionController(IConfiguration _configuration)
        {
            configuration = _configuration ?? throw new ArgumentNullException(nameof(_configuration));
        }

        // Helper method to populate Account dropdown
        private List<AccountDropDownModel> SetAccountDropDown()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using SqlConnection connection = new(connectionString);
            connection.Open();
            using SqlCommand command = new("SP_AccountDropDown", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using SqlDataReader reader = command.ExecuteReader();
            DataTable dataTable = new();
            dataTable.Load(reader);

            return dataTable.AsEnumerable().Select(row => new AccountDropDownModel
            {
                Id = row.Field<int>("Id"),
                AccountName = row.Field<string>("Name") ?? string.Empty,
                GroupName = row["GroupName"] != DBNull.Value ? row.Field<string>("GroupName")! : string.Empty
            }).ToList();
        }

        // Generate sequential bill number
        private string GenerateSequentialBillNo(string type)
        {
            string prefix = type == "GoldPurchaseRate" ? "GPR" : "GSR";
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            int lastNumber = 0;

            using SqlConnection connection = new(connectionString);
            connection.Open();
            using SqlCommand command = new($"SELECT MAX(CAST(SUBSTRING(BillNo, 4, LEN(BillNo)) AS INT)) FROM RateCutTransactions WHERE BillNo LIKE '{prefix}%'", connection);
            object? result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                lastNumber = Convert.ToInt32(result);
            }
            lastNumber++;
            string billNo = $"{prefix}{lastNumber:D4}";
            if (billNo.Length > 20) throw new Exception("BillNo exceeds NVARCHAR(20) length.");
            return billNo;
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string? type)
        {
            if (string.IsNullOrEmpty(type) || !new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
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

        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using SqlConnection connection = new(connectionString);
            connection.Open();

            // Fetch current balance from Account table
            using SqlCommand balanceCommand = new("SP_GetPreviousBalance", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            balanceCommand.Parameters.AddWithValue("@AccountId", accountId);
            using SqlDataReader balanceReader = balanceCommand.ExecuteReader();
            decimal fine = 0;
            decimal amount = 0;
            if (balanceReader.Read())
            {
                fine = balanceReader.GetDecimal("Fine");
                amount = balanceReader.GetDecimal("Amount");
            }
            balanceReader.Close();

            // Fetch the date of the last balance-affecting transaction
            string query = @"
                SELECT MAX(LastDate) AS LastBalanceDate
                FROM (
                    SELECT MAX(Date) AS LastDate FROM Transactions WHERE AccountId = @AccountId
                    UNION
                    SELECT MAX(Date) FROM AmountTransactions WHERE AccountId = @AccountId
                    UNION
                    SELECT MAX(Date) FROM MetalTransactions WHERE AccountId = @AccountId
                    UNION
                    SELECT MAX(Date) FROM RateCutTransactions WHERE AccountId = @AccountId
                ) AS AllDates";

            using SqlCommand dateCommand = new(query, connection);
            dateCommand.Parameters.AddWithValue("@AccountId", accountId);
            object? lastDateResult = dateCommand.ExecuteScalar();
            DateTime? lastBalanceDate = lastDateResult != null && lastDateResult != DBNull.Value
                ? Convert.ToDateTime(lastDateResult)
                : null;

            return Json(new
            {
                fine,
                amount,
                lastBalanceDate = lastBalanceDate?.ToString("yyyy-MM-dd")
            });
        }

        public IActionResult GenrateRateCutVoucher(string type = "GoldPurchaseRate", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.AccountDropDown = SetAccountDropDown();
            var model = new RateCutTransactionModel
            {
                Type = type,
                Date = DateTime.Now,
                Weight = 0,
                Tunch = 0,
                Rate = 0,
                Amount = 0
            };

            if (!string.IsNullOrEmpty(billNo))
            {
                string? connectionString = configuration.GetConnectionString("ConnectionString");
                using SqlConnection connection = new(connectionString);
                connection.Open();
                using SqlCommand command = new("SP_RateCutTransaction_SelectByBillNo", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.AddWithValue("@BillNo", billNo);

                using SqlDataReader reader = command.ExecuteReader();
                DataTable table = new();
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
                model.Weight = row.Field<decimal>("Weight");
                model.Tunch = row.Field<decimal>("Tunch");
                model.Rate = row.Field<decimal>("Rate");
                model.Amount = row.Field<decimal>("Amount");
                model.Narration = row["Narration"] != DBNull.Value ? row.Field<string>("Narration") : null;

                ViewBag.SelectedAccountId = model.AccountId;
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.AccountId = accountId.Value;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddRateCutTransaction(RateCutTransactionModel model, int? SelectedAccountId)
        {
            ViewBag.AccountDropDown = SetAccountDropDown();

            // Set Type from form if not provided in model
            model.Type ??= Request.Form["Type"].FirstOrDefault() ?? "GoldPurchaseRate";

            // Validation
            if (!SelectedAccountId.HasValue)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
            }
            else
            {
                model.AccountId = SelectedAccountId.Value;
            }

            if (string.IsNullOrEmpty(model.BillNo) || model.BillNo.Length > 20)
            {
                ModelState.AddModelError("BillNo", "Bill Number is required and must not exceed 20 characters.");
            }

            if (model.Weight <= 0)
            {
                ModelState.AddModelError("Weight", "Weight must be greater than 0.");
            }

            if (model.Tunch <= 0 || model.Tunch > 100)
            {
                ModelState.AddModelError("Tunch", "Tunch must be between 0 and 100.");
            }

            if (model.Rate <= 0)
            {
                ModelState.AddModelError("Rate", "Rate must be greater than 0.");
            }

            if (model.Narration?.Length > 500)
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
                using SqlConnection connection = new(connectionString);
                connection.Open();

                // Calculate Amount based on Fine Gold (Weight * Tunch / 100 * Rate)
                decimal fineGold = (model.Weight * model.Tunch) / 100;
                model.Amount = fineGold * model.Rate;

                // Insert or Update RateCutTransaction
                using SqlCommand command = model.Id > 0
                    ? new("SP_RateCutTransaction_Update", connection)
                    : new("SP_RateCutTransaction_Insert", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                if (model.Id > 0) command.Parameters.AddWithValue("@Id", model.Id);
                command.Parameters.AddWithValue("@BillNo", model.BillNo);
                command.Parameters.AddWithValue("@Date", model.Date);
                command.Parameters.AddWithValue("@AccountId", model.AccountId);
                command.Parameters.AddWithValue("@Type", model.Type);
                command.Parameters.AddWithValue("@Weight", model.Weight);
                command.Parameters.AddWithValue("@Tunch", model.Tunch);
                command.Parameters.AddWithValue("@Rate", model.Rate);
                command.Parameters.AddWithValue("@Amount", model.Amount);
                command.Parameters.AddWithValue("@Narration", string.IsNullOrEmpty(model.Narration) ? DBNull.Value : model.Narration);
                command.ExecuteNonQuery();

                // Update Account balance
                var account = SetAccountDropDown().FirstOrDefault(a => a.Id == model.AccountId);
                if (account != null)
                {
                    decimal fineAdjustment = fineGold; // Fine metal
                    decimal amountAdjustment = model.Amount;

                    // Adjust based on GroupName and Type
                    if (account.GroupName == "Supplier")
                    {
                        if (model.Type == "GoldPurchaseRate")
                        {
                            fineAdjustment = -fineGold;  // Decrease Fine
                            // amountAdjustment remains positive (Increase Amount)
                        }
                        else if (model.Type == "GoldSaleRate")
                        {
                            // fineAdjustment remains positive (Increase Fine)
                            amountAdjustment = -model.Amount; // Decrease Amount
                        }
                    }
                    else if (account.GroupName == "Customer")
                    {
                        if (model.Type == "GoldPurchaseRate")
                        {
                            // fineAdjustment remains positive (Increase Fine)
                            amountAdjustment = -model.Amount; // Decrease Amount
                        }
                        else if (model.Type == "GoldSaleRate")
                        {
                            fineAdjustment = -fineGold;  // Decrease Fine
                            // amountAdjustment remains positive (Increase Amount)
                        }
                    }

                    using SqlCommand updateCommand = new(
                        "UPDATE Account SET Fine = ISNULL(Fine, 0) + @FineAdjustment, Amount = ISNULL(Amount, 0) + @AmountAdjustment WHERE AccountId = @AccountId",
                        connection);
                    updateCommand.Parameters.AddWithValue("@FineAdjustment", fineAdjustment);
                    updateCommand.Parameters.AddWithValue("@AmountAdjustment", amountAdjustment);
                    updateCommand.Parameters.AddWithValue("@AccountId", model.AccountId);
                    updateCommand.ExecuteNonQuery();
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