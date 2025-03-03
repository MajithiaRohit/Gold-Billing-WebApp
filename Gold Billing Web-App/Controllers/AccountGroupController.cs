using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

public class AccountGroupController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public AccountGroupController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("ConnectionString")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string is missing.");
    }

    public IActionResult AccountGroupList()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand("SP_GroupAccount_SelectAll", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return View(table);
    }

    public IActionResult AddEditAccountGroup(int Id)
    {
        if (Id <= 0) return View(new AccountGroupModel());

        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand("SP_GroupAccount_SelectByID", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@Id", Id);

        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);

        if (table.Rows.Count == 0) return NotFound();

        var accountGroupModel = new AccountGroupModel
        {
            Id = table.Rows[0].Field<int>("Id"),
            GroupName = table.Rows[0].Field<string>("GroupName") ?? string.Empty
        };
        return View(accountGroupModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveAddEditAccountGroup(AccountGroupModel accountGroup)
    {
        if (!ModelState.IsValid)
        {
            return View("AddEditAccountGroup", accountGroup);
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                accountGroup.Id == null ? "SP_GroupAccount_Insert" : "SP_GroupAccount_Update",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (accountGroup.Id.HasValue)
                command.Parameters.AddWithValue("@Id", accountGroup.Id);

            command.Parameters.AddWithValue("@GroupName", accountGroup.GroupName);
            command.ExecuteNonQuery();

            TempData["SuccessMessage"] = accountGroup.Id == null
                ? "Account group added successfully!"
                : "Account group updated successfully!";
            return RedirectToAction("AccountGroupList");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
            return View("AddEditAccountGroup", accountGroup);
        }
    }

    public IActionResult DeleteAccountGroup(int Id)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SP_GroupAccount_Delete", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@Id", Id);
            command.ExecuteNonQuery();

            TempData["SuccessMessage"] = "Account group deleted successfully!";
        }
        catch (SqlException ex) when (ex.Number == 547) // FK constraint violation
        {
            TempData["ErrorMessage"] = "Cannot delete this account group because it is referenced by other records.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Deletion failed: {ex.Message}";
        }
        return RedirectToAction("AccountGroupList");
    }
}