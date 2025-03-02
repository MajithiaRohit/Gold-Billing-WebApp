using System.Data;
using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gold_Billing_Web_App.Controllers
{
    public class AccountController : Controller
    {
        #region Configration
        private readonly IConfiguration configuration;
        public AccountController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }
        #endregion

        public List<AccountGroupDropDownModel> setGroupDropDown()
        {
            #region Display  DropDownList
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            SqlConnection connection1 = new SqlConnection(connectionString);
            connection1.Open();
            SqlCommand command1 = connection1.CreateCommand();
            command1.CommandType = CommandType.StoredProcedure;
            command1.CommandText = "SP_AccountGroupDropDown";
            
            SqlDataReader reader1 = command1.ExecuteReader();
            DataTable dataTable1 = new DataTable();
            dataTable1.Load(reader1);
            connection1.Close();

            List<AccountGroupDropDownModel> group = new List<AccountGroupDropDownModel>();

            foreach (DataRow dataRow in dataTable1.Rows)
            {
                AccountGroupDropDownModel groupDropDownModel = new AccountGroupDropDownModel();
                groupDropDownModel.Id = Convert.ToInt32(dataRow["Id"]);
                groupDropDownModel.GroupName = dataRow["GroupName"].ToString()!;
                group.Add(groupDropDownModel);
            }

            return group;
            #endregion

        }



        #region Account List
        public IActionResult AccountList()
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
            command.CommandText = "SP_Account_SelectAll";
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return View(table);
        }
        #endregion

       
        #region Delete
        public IActionResult DeleteAccount(int AccountId)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand("SP_Account_Delete", con);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@AccountId", AccountId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("AccountList");
        }
        #endregion


        #region Add/Edit Account Form
        public IActionResult AddEditAccount(int AccountId)
        {
            if (AccountId > 0)
            {
                string? connectionString = this.configuration.GetConnectionString("ConnectionString");
                SqlConnection connection = new SqlConnection(connectionString);
                ViewBag.groupList = setGroupDropDown();
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_Account_SelectByID";
                command.Parameters.AddWithValue("@AccountId", AccountId);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);
                connection.Close();

                AccountModel accountModel = new AccountModel();

                foreach (DataRow dataRow in table.Rows)
                {
                    accountModel.AccountId = Convert.ToInt32(dataRow["AccountId"]);
                    accountModel.AccountName = dataRow["AccountName"].ToString()!;
                    accountModel.AccountGroupId = Convert.ToInt32(dataRow["AccountGroupId"]);
                    accountModel.Address = dataRow["Address"].ToString()!;
                    accountModel.City = dataRow["City"].ToString()!;
                    accountModel.Pincode = dataRow["Pincode"].ToString()!;
                    accountModel.MobileNo = dataRow["MobileNo"].ToString()!;
                    accountModel.PhoneNo = dataRow["PhoneNo"].ToString()!;
                    accountModel.Email = dataRow["Email"].ToString()!;
                    accountModel.Fine = Convert.ToDecimal(dataRow["Fine"]);
                    accountModel.Amount = Convert.ToDecimal(dataRow["Amount"]);
                }

                return View(accountModel);
            }
            else
            {
                ViewBag.groupList = setGroupDropDown();
                return View(new AccountModel());
            }
        }
        #endregion

        #region Save Method
        public IActionResult saveAddEditAccount(AccountModel account)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            try
            {
                SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                ViewBag.groupList = setGroupDropDown();
                command.CommandType = CommandType.StoredProcedure;

                if (account.AccountId == null)
                {
                    command.CommandText = "SP_Account_Insert";
                }
                else
                {
                   
                    command.CommandText = "SP_Account_Update";
                    command.Parameters.AddWithValue("@AccountId", account.AccountId);
                }
                command.Parameters.AddWithValue("@Date", DateTime.Now);
                command.Parameters.AddWithValue("@AccountName", account.AccountName);
                command.Parameters.AddWithValue("@AccountGroupId", account.AccountGroupId);
                command.Parameters.AddWithValue("@Address", account.Address);
                command.Parameters.AddWithValue("@City", account.City);
                command.Parameters.AddWithValue("@Pincode", account.Pincode);
                command.Parameters.AddWithValue("@MobileNo", account.MobileNo);
                command.Parameters.AddWithValue("@PhoneNo", account.PhoneNo);
                command.Parameters.AddWithValue("@Email", account.Email);
                command.Parameters.AddWithValue("@Fine", account.Fine);
                command.Parameters.AddWithValue("@Amount", account.Amount);
                command.ExecuteNonQuery();
                connection.Close();
               
                return RedirectToAction("AccountList");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return View("AddEditAccount", account);
            }
        }
        #endregion
    }
}

