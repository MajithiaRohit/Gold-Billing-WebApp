using System;
using System.Data;
using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        private readonly IConfiguration configuration;

        public OpeningStockController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }

        private string GenerateSequentialBillNo()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            string prefix = "BILL";
            int lastNumber = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT MAX(CAST(SUBSTRING(BillNo, 5, LEN(BillNo)) AS INT)) FROM OpeningStock WHERE BillNo LIKE 'BILL%'";

                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    lastNumber = Convert.ToInt32(result);
                }

                lastNumber += 1;
                return $"{prefix}{lastNumber:D4}";
            }
        }

        public List<ItemDropDownModel> SetItemDropDown()
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_ItemDropDown";

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
            ViewBag.itemDropDown = SetItemDropDown();
            var model = new OpeningStockModel
            {
                BillNo = GenerateSequentialBillNo(),
                Date = DateTime.Now
            };
            CalculateDerivedFields(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddOpeningStock(OpeningStockModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.itemDropDown = SetItemDropDown();
                return View(model);
            }

            string? connectionString = configuration.GetConnectionString("ConnectionString");
            try
            {
                CalculateDerivedFields(model);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = connection.CreateCommand();
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "SP_OpeningStock_Insert";

                    command.Parameters.AddWithValue("@Date", model.Date);
                    command.Parameters.AddWithValue("@BillNo", model.BillNo);
                    command.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ItemId", model.ItemId);
                    command.Parameters.AddWithValue("@Pc", model.Pc ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Weight", model.Weight ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Less", model.Less ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@NetWt", model.NetWt ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Tunch", model.Tunch);
                    command.Parameters.AddWithValue("@Wastage", model.Wastage);
                    command.Parameters.AddWithValue("@TW", model.TW);
                    command.Parameters.AddWithValue("@Rate", model.Rate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Fine", model.Fine ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Amount", model.Amount ?? (object)DBNull.Value);

                    command.ExecuteNonQuery();
                }
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.itemDropDown = SetItemDropDown();
                return View(model);
            }
        }

        public IActionResult EditOpeningStock(int id)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            ViewBag.itemDropDown = SetItemDropDown();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_OpeningStock_SelectById";
                command.Parameters.AddWithValue("@Id", id);

                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return NotFound();
                }

                DataRow row = table.Rows[0];
                var model = new OpeningStockModel
                {
                    Id = row.Field<int>("Id"),
                    BillNo = row.Field<string>("BillNo")!,
                    Date = row.Field<DateTime>("Date"),
                    Narration = row.Field<string>("Narration"),
                    ItemId = row.Field<int>("ItemId"),
                    ItemName = row.Field<string>("Name")!, // Assuming join with Item table
                    Pc = row["Pc"] != DBNull.Value ? row.Field<int>("Pc") : null,
                    Weight = row["Weight"] != DBNull.Value ? row.Field<decimal>("Weight") : null,
                    Less = row["Less"] != DBNull.Value ? row.Field<decimal>("Less") : null,
                    NetWt = row["NetWt"] != DBNull.Value ? row.Field<decimal>("NetWt") : null,
                    Tunch = row.Field<decimal>("Tunch"),
                    Wastage = row.Field<decimal>("Wastage"),
                    TW = row.Field<decimal>("TW"),
                    Rate = row["Rate"] != DBNull.Value ? row.Field<decimal>("Rate") : null,
                    Fine = row["Fine"] != DBNull.Value ? row.Field<decimal>("Fine") : null,
                    Amount = row["Amount"] != DBNull.Value ? row.Field<decimal>("Amount") : null
                };
                return View("AddOpeningStock", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditOpeningStock(OpeningStockModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.itemDropDown = SetItemDropDown();
                return View("AddOpeningStock", model);
            }

            string? connectionString = configuration.GetConnectionString("ConnectionString");
            try
            {
                CalculateDerivedFields(model);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = connection.CreateCommand();
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "SP_OpeningStock_Update";

                    command.Parameters.AddWithValue("@Id", model.Id);
                    command.Parameters.AddWithValue("@Date", model.Date);
                    command.Parameters.AddWithValue("@BillNo", model.BillNo);
                    command.Parameters.AddWithValue("@Narration", model.Narration ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ItemId", model.ItemId);
                    command.Parameters.AddWithValue("@Pc", model.Pc ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Weight", model.Weight ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Less", model.Less ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@NetWt", model.NetWt ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Tunch", model.Tunch);
                    command.Parameters.AddWithValue("@Wastage", model.Wastage);
                    command.Parameters.AddWithValue("@TW", model.TW);
                    command.Parameters.AddWithValue("@Rate", model.Rate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Fine", model.Fine ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Amount", model.Amount ?? (object)DBNull.Value);

                    command.ExecuteNonQuery();
                }
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                ViewBag.itemDropDown = SetItemDropDown();
                return View("AddOpeningStock", model);
            }
        }

        public IActionResult DeleteOpeningStock(int id)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand("SP_OpeningStock_Delete", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Id", id);
                connection.Open();
                command.ExecuteNonQuery();
            }
            return RedirectToAction("ViewStock");
        }

        public IActionResult ViewStock()
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
                return View(table);
            }
        }

        private void CalculateDerivedFields(OpeningStockModel model)
        {
            model.NetWt = (model.Weight ?? 0) - (model.Less ?? 0);
            model.TW = model.Tunch + model.Wastage;
            model.Fine = (model.NetWt ?? 0) * (model.TW / 100);
            model.Amount = (model.Fine ?? 0) * (model.Rate ?? 0);
        }
    }
}