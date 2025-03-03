using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

public class AccountController : Controller
{
    private readonly IConfiguration configuration;
    public AccountController(IConfiguration _configuration)
    {
        configuration = _configuration;
    }

    private List<AccountGroupDropDownModel> SetGroupDropDown()
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_AccountGroupDropDown";
            SqlDataReader reader = command.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(reader);

            List<AccountGroupDropDownModel> group = new List<AccountGroupDropDownModel>();
            foreach (DataRow dataRow in dataTable.Rows)
            {
                group.Add(new AccountGroupDropDownModel
                {
                    Id = Convert.ToInt32(dataRow["Id"]),
                    GroupName = dataRow["GroupName"].ToString()!
                });
            }
            return group;
        }
    }

    public IActionResult AccountList()
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("Database connection string is missing or invalid.");
        }

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_Account_SelectAll";
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return View(table);
        }
    }

    public IActionResult DeleteAccount(int AccountId)
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
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

    public IActionResult AddEditAccount(int AccountId)
    {
        ViewBag.groupList = SetGroupDropDown();

        if (AccountId > 0)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_Account_SelectByID";
                command.Parameters.AddWithValue("@AccountId", AccountId);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return NotFound();
                }

                AccountModel accountModel = new AccountModel
                {
                    AccountId = Convert.ToInt32(table.Rows[0]["AccountId"]),
                    AccountName = table.Rows[0]["AccountName"].ToString()!,
                    AccountGroupId = Convert.ToInt32(table.Rows[0]["AccountGroupId"]),
                    Address = table.Rows[0]["Address"].ToString()!,
                    City = table.Rows[0]["City"].ToString()!,
                    Pincode = table.Rows[0]["Pincode"].ToString()!,
                    MobileNo = table.Rows[0]["MobileNo"].ToString()!,
                    PhoneNo = table.Rows[0]["PhoneNo"].ToString()!,
                    Email = table.Rows[0]["Email"].ToString()!,
                    Fine = Convert.ToDecimal(table.Rows[0]["Fine"]),
                    Amount = Convert.ToDecimal(table.Rows[0]["Amount"])
                };
                return View(accountModel);
            }
        }
        return View(new AccountModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult saveAddEditAccount(AccountModel account)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.groupList = SetGroupDropDown();
            return View("AddEditAccount", account);
        }

        string? connectionString = configuration.GetConnectionString("ConnectionString");
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
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
                command.Parameters.AddWithValue("@Address", account.Address ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@City", account.City ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Pincode", account.Pincode ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@MobileNo", account.MobileNo);
                command.Parameters.AddWithValue("@PhoneNo", account.PhoneNo ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Email", account.Email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Fine", account.Fine);
                command.Parameters.AddWithValue("@Amount", account.Amount);

                command.ExecuteNonQuery();
            }
            return RedirectToAction("AccountList");
        }
        catch (Exception ex)
        {
            ViewBag.groupList = SetGroupDropDown();
            ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
            return View("AddEditAccount", account);
        }
    }
}