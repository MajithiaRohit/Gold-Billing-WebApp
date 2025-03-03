using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.Generic;

namespace Gold_Billing_Web_App.Controllers
{
    public class PurchaseController : Controller
    {
        private readonly IConfiguration _configuration;

        public PurchaseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult GeneratePurchaseBill()
        {
            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
            {
                conn.Open();

                var accounts = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand("SELECT AccountId, AccountName FROM Account", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            accounts.Add(new { AccountId = reader.GetInt32(0), AccountName = reader.GetString(1) });
                        }
                    }
                }
                ViewBag.Accounts = accounts;

                var items = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand("SELECT Id, Name FROM Item", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new { Id = reader.GetInt32(0), ItemName = reader.GetString(1) });
                        }
                    }
                }
                ViewBag.Items = items;
            }
            return View();
        }

        public IActionResult PurchaseList()
        {
            var purchases = new List<PurchaseViewModel>();
            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT p.Id, p.Date, p.AccountId, a.AccountName, p.Narration, p.TransactionType, p.ItemId, i.Name as ItemName, p.Pc, p.Weight, p.Less, p.NetWt, p.Tunch, p.Wastage, p.TW, p.Rate, p.Fine, p.Amount FROM Purchase p LEFT JOIN Account a ON p.AccountId = a.AccountId LEFT JOIN Item i ON p.ItemId = i.Id", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var purchase = new PurchaseViewModel
                            {
                                Date = reader.GetDateTime(1),
                                AccountId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                Narration = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Items = new List<PurchaseItem>
                                {
                                    new PurchaseItem
                                    {
                                        TransactionType = reader.GetString(5),
                                        ItemId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                        Pc = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                                        Weight = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                                        Less = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                                        NetWt = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                                        Tunch = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                        Wastage = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                                        TW = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14),
                                        Rate = reader.IsDBNull(15) ? 0 : reader.GetDecimal(15),
                                        Fine = reader.IsDBNull(16) ? 0 : reader.GetDecimal(16),
                                        Amount = reader.IsDBNull(17) ? 0 : reader.GetDecimal(17)
                                    }
                                }
                            };
                            purchases.Add(purchase);
                        }
                    }
                }
            }
            return View(purchases);
        }

        [HttpPost]
        public IActionResult SavePurchase([FromBody] PurchaseViewModel model)
        {
            try
            {
                if (!model.Date.HasValue || model.Date.Value < new DateTime(1753, 1, 1) || model.Date.Value > new DateTime(9999, 12, 31))
                {
                    return Json(new { success = false, message = "Please select a valid date between 1753 and 9999" });
                }

                string billNo;
                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("sp_InsertPurchase", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Date", model.Date.Value);
                        cmd.Parameters.AddWithValue("@AccountId", model.AccountId == 0 ? (object)DBNull.Value : model.AccountId);
                        cmd.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);

                        DataTable itemsTable = CreatePurchaseItemsTable(model.Items);
                        SqlParameter parameter = cmd.Parameters.AddWithValue("@PurchaseItems", itemsTable);
                        parameter.SqlDbType = SqlDbType.Structured;
                        parameter.TypeName = "PurchaseItemType";

                        billNo = cmd.ExecuteScalar()?.ToString();
                    }
                }
                return Json(new { success = true, message = "Purchase saved successfully", billNo = billNo });
            }
            catch (SqlException ex) when (ex.Number == 242)
            {
                return Json(new { success = false, message = "Invalid date format" });
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return Json(new { success = false, message = "Invalid Account or Item reference" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UpdatePurchase(int id, [FromBody] PurchaseViewModel model)
        {
            try
            {
                if (!model.Date.HasValue || model.Date.Value < new DateTime(1753, 1, 1) || model.Date.Value > new DateTime(9999, 12, 31))
                {
                    return Json(new { success = false, message = "Please select a valid date between 1753 and 9999" });
                }

                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("sp_UpdatePurchase", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Date", model.Date.Value);
                        cmd.Parameters.AddWithValue("@AccountId", model.AccountId == 0 ? (object)DBNull.Value : model.AccountId);
                        cmd.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);

                        DataTable itemsTable = CreatePurchaseItemsTable(model.Items);
                        SqlParameter parameter = cmd.Parameters.AddWithValue("@PurchaseItems", itemsTable);
                        parameter.SqlDbType = SqlDbType.Structured;
                        parameter.TypeName = "PurchaseItemType";

                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { success = true, message = "Purchase updated successfully" });
            }
            catch (SqlException ex) when (ex.Number == 242)
            {
                return Json(new { success = false, message = "Invalid date format" });
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return Json(new { success = false, message = "Invalid Account or Item reference" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeletePurchase(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("sp_DeletePurchase", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { success = true, message = "Purchase deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private DataTable CreatePurchaseItemsTable(List<PurchaseItem> items)
        {
            DataTable table = new DataTable();
            table.Columns.Add("TransactionType", typeof(string));
            table.Columns.Add("ItemId", typeof(int));
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

            foreach (var item in items)
            {
                table.Rows.Add(
                    item.TransactionType,
                    item.ItemId == 0 ? (object)DBNull.Value : item.ItemId,
                    item.Pc,
                    item.Weight,
                    item.Less,
                    item.NetWt,
                    item.Tunch,
                    item.Wastage,
                    item.TW,
                    item.Rate,
                    item.Fine,
                    item.Amount
                );
            }
            return table;
        }
    }

    public class PurchaseViewModel
    {
        public DateTime? Date { get; set; }
        public int AccountId { get; set; }
        public string? Narration { get; set; }
        public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    }

    public class PurchaseItem
    {
        public string TransactionType { get; set; }
        public int ItemId { get; set; }
        public int Pc { get; set; }
        public decimal Weight { get; set; }
        public decimal Less { get; set; }
        public decimal NetWt { get; set; }
        public decimal Tunch { get; set; }
        public decimal Wastage { get; set; }
        public decimal TW { get; set; }
        public decimal Rate { get; set; }
        public decimal Fine { get; set; }
        public decimal Amount { get; set; }
    }
}