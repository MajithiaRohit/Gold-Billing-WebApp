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

        #region Generate Sequential BillNo
        private string GenerateSequentialBillNo()
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
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

                lastNumber += 1; // Increment by 1
                connection.Close();
            }

            return $"{prefix}{lastNumber:D4}"; // e.g., BILL0001, BILL0002
        }
        #endregion

        #region Dropdown for Items
        public List<ItemDropDownModel> setItemDropDown()
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            SqlCommand command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_ItemDropDown";

            SqlDataReader reader = command.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(reader);
            connection.Close();

            List<ItemDropDownModel> items = new List<ItemDropDownModel>();

            foreach (DataRow dataRow in dataTable.Rows)
            {
                ItemDropDownModel item = new ItemDropDownModel
                {
                    Id = Convert.ToInt32(dataRow["Id"]),
                    ItemName = dataRow["Name"].ToString()!
                };
                items.Add(item);
            }

            return items;
        }
        #endregion

        #region Add Opening Stock
        public IActionResult AddOpeningStock()
        {
            ViewBag.itemDropDown = setItemDropDown();
            var model = new OpeningStockModel
            {
                BillNo = GenerateSequentialBillNo(),
                Date = DateTime.Now
            };
            // Set default calculated values (optional, can be left blank for user input)
            CalculateDerivedFields(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddOpeningStock(OpeningStockModel model)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            try
            {
                // Calculate derived fields before saving
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
                    command.Parameters.AddWithValue("@Pc", model.Pc);
                    command.Parameters.AddWithValue("@Weight", model.Weight);
                    command.Parameters.AddWithValue("@Less", model.Less);
                    command.Parameters.AddWithValue("@NetWt", model.NetWt);
                    command.Parameters.AddWithValue("@Tunch", model.Tunch);
                    command.Parameters.AddWithValue("@Wastage", model.Wastage);
                    command.Parameters.AddWithValue("@TW", model.TW);
                    command.Parameters.AddWithValue("@Rate", model.Rate);
                    command.Parameters.AddWithValue("@Fine", model.Fine);
                    command.Parameters.AddWithValue("@Amount", model.Amount);

                    command.ExecuteNonQuery();
                    connection.Close();
                }
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                ViewBag.itemDropDown = setItemDropDown();
                return View(model);
            }
        }
        #endregion

        #region Edit Opening Stock
        public IActionResult EditOpeningStock(int id)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            ViewBag.itemDropDown = setItemDropDown();

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
                connection.Close();

                if (table.Rows.Count > 0)
                {
                    DataRow row = table.Rows[0];
                    var model = new OpeningStockModel
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        BillNo = row["BillNo"].ToString()!,
                        Date = Convert.ToDateTime(row["Date"]),
                        Narration = row["Narration"].ToString(),
                        //ItemId = Convert.ToInt32(row["ItemId"]),
                        //ItemName = row["Name"].ToString()!, // From the JOIN with Item
                        Pc = Convert.ToInt32(row["Pc"]),
                        Weight = Convert.ToDecimal(row["Weight"]),
                        Less = Convert.ToDecimal(row["Less"]),
                        NetWt = Convert.ToDecimal(row["NetWt"]),
                        Tunch = Convert.ToDecimal(row["Tunch"]),
                        Wastage = Convert.ToDecimal(row["Wastage"]),
                        TW = Convert.ToDecimal(row["TW"]),
                        Rate = Convert.ToDecimal(row["Rate"]),
                        Fine = Convert.ToDecimal(row["Fine"]),
                        Amount = Convert.ToDecimal(row["Amount"])
                    };
                    return View("AddOpeningStock", model); // Reuse the same view for editing
                }
            }
            return NotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditOpeningStock(OpeningStockModel model)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            try
            {
                // Calculate derived fields before updating
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
                   // command.Parameters.AddWithValue("@ItemId", model.ItemId);
                    command.Parameters.AddWithValue("@Pc", model.Pc);
                    command.Parameters.AddWithValue("@Weight", model.Weight);
                    command.Parameters.AddWithValue("@Less", model.Less);
                    command.Parameters.AddWithValue("@NetWt", model.NetWt);
                    command.Parameters.AddWithValue("@Tunch", model.Tunch);
                    command.Parameters.AddWithValue("@Wastage", model.Wastage);
                    command.Parameters.AddWithValue("@TW", model.TW);
                    command.Parameters.AddWithValue("@Rate", model.Rate);
                    command.Parameters.AddWithValue("@Fine", model.Fine);
                    command.Parameters.AddWithValue("@Amount", model.Amount);

                    command.ExecuteNonQuery();
                    connection.Close();
                }
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                ViewBag.itemDropDown = setItemDropDown();
                return View("AddOpeningStock", model);
            }
        }
        #endregion

        #region Delete Opening Stock
        public IActionResult DeleteOpeningStock(int id)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
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
        #endregion

        #region View Stock List
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
        #endregion

        #region Helper Method for Calculations
        private void CalculateDerivedFields(OpeningStockModel model)
        {
            model.NetWt = model.Weight - model.Less;
            model.TW = model.Tunch + model.Wastage;
            model.Fine = model.NetWt * (model.TW / 100); // Assuming TW is a percentage
            model.Amount = model.Fine * model.Rate;
        }
        #endregion
    }
}