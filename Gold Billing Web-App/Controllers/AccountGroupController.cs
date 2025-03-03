using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

public class AccountGroupController : Controller
{
    #region Configuration
    private readonly IConfiguration configuration;
    public AccountGroupController(IConfiguration _configuration)
    {
        configuration = _configuration;
    }
    #endregion

    #region AccountGroupList
    public IActionResult AccountGroupList()
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
            command.CommandText = "SP_GroupAccount_SelectAll";
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return View(table);
        }
    }
    #endregion

    #region Add/Edit
    public IActionResult AddEditAccountGroup(int Id)
    {
        if (Id > 0)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_GroupAccount_SelectByID";
                command.Parameters.AddWithValue("@Id", Id);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    return NotFound();
                }

                AccountGroupModel accountGroupModel = new AccountGroupModel
                {
                    Id = Convert.ToInt32(table.Rows[0]["Id"]),
                    GroupName = table.Rows[0]["GroupName"].ToString()!
                };

                return View(accountGroupModel);
            }
        }
        return View(new AccountGroupModel());
    }
    #endregion

    #region Save Method
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult saveAddEditAccountGroup(AccountGroupModel accountGroup)
    {
        if (!ModelState.IsValid)
        {
            return View("AddEditAccountGroup", accountGroup);
        }

        string? connectionString = configuration.GetConnectionString("ConnectionString");
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;

                if (accountGroup.Id == null)
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
            }
            return RedirectToAction("AccountGroupList");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
            return View("AddEditAccountGroup", accountGroup);
        }
    }
    #endregion

    #region Delete
    public IActionResult DeleteAccountGroup(int Id)
    {
        string? connectionString = configuration.GetConnectionString("ConnectionString");
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