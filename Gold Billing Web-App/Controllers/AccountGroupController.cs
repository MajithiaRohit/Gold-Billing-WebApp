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

        #region AccountGroupList
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
        #endregion
        public IActionResult AddEditAccountGroup(int Id)
        {
            if (Id > 0)
            {
                string? connectionString = this.configuration.GetConnectionString("ConnectionString");
                SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_GroupAccount_SelectByID";
                command.Parameters.AddWithValue("@Id", Id);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);
                connection.Close();

                AccountGroupModel accountGroupModel = new AccountGroupModel();

                foreach (DataRow dataRow in table.Rows)
                {
                    accountGroupModel.Id = Convert.ToInt32(dataRow["Id"]);
                    accountGroupModel.GroupName = dataRow["GroupName"].ToString()!;
                }

                return View(accountGroupModel);
            }
            else
            {
                return View(new AccountGroupModel());
            }
        }

        #region save method
        public IActionResult saveAddEditAccountGroup(AccountGroupModel accountGroup)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
                try
                {
                    SqlConnection connection = new SqlConnection(connectionString);
                    connection.Open();
                    SqlCommand command = connection.CreateCommand();
                    command.CommandType = CommandType.StoredProcedure;

                    if (accountGroup.Id == null )
                    {
                        command.CommandText = "SP_GroupAccount_Insert";
                    }
                    else
                    {
                        command.CommandText = "SP_GroupAccount_Update";
                        command.Parameters.AddWithValue("@Id", accountGroup.Id);
                    }

                    command.Parameters.AddWithValue("@GroupName", accountGroup.GroupName);

                    command.ExecuteNonQuery();
                    connection.Close();

                    return RedirectToAction("AccountGroupList");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                    return View("AccountGroupList", accountGroup);
                }
            
        }
        #endregion

        #region Delete
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
        #endregion
    }
}
