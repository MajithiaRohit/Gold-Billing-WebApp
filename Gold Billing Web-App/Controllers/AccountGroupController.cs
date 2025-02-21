using System.Data;
using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Gold_Billing_Web_App.Controllers
{
    public class AccountGroupController : Controller
    {
        #region Configration
        private readonly IConfiguration configuration;
        public AccountGroupController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }
        #endregion
        public IActionResult AccountGroupList()
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Database connection string is missing or invalid.");
            }
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            SqlCommand command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_GroupAccount_SelectAll";
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return View(table);
        }
        [HttpPost]
        public IActionResult AddEditAccountGroup(AccountGroupModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = con;
                cmd.CommandType = CommandType.StoredProcedure;

                if (model.Id > 0)
                {
                    // If ID exists, update the record
                    cmd.CommandText = "SP_GroupAccount_Update";
                    cmd.Parameters.AddWithValue("@Id", model.Id);
                }
                else
                {
                    // If ID is 0 or missing, insert a new record
                    cmd.CommandText = "SP_GroupAccount_Insert";
                }
                cmd.Parameters.AddWithValue("@GroupName", model.GroupName);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("AccountGroupList");
        }


        public IActionResult DeleteAccountGroup(int Id)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand("SP_GroupAccount_Delete", con);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Id", Id);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("AccountGroupList");
        }
    }
}
