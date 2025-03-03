using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public AccountController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("ConnectionString")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string is missing.");
    }

    private List<AccountGroupDropDownModel> SetGroupDropDown()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand("SP_AccountGroupDropDown", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        using var reader = command.ExecuteReader();
        var dataTable = new DataTable();
        dataTable.Load(reader);

        return dataTable.AsEnumerable()
            .Select(row => new AccountGroupDropDownModel
            {
                Id = row.Field<int>("Id"),
                GroupName = row.Field<string>("GroupName") ?? string.Empty
            })
            .ToList();
    }

    public IActionResult AccountList()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand("SP_Account_SelectAll", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return View(table);
    }

    public IActionResult AddEditAccount(int AccountId)
    {
        ViewBag.groupList = SetGroupDropDown();

        if (AccountId <= 0) return View(new AccountModel());

        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand("SP_Account_SelectByID", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@AccountId", AccountId);

        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);

        if (table.Rows.Count == 0) return NotFound();

        var row = table.Rows[0];
        return View(new AccountModel
        {
            AccountId = row.Field<int>("AccountId"),
            AccountName = row.Field<string>("AccountName") ?? string.Empty,
            AccountGroupId = row.Field<int>("AccountGroupId"),
            Address = row.Field<string>("Address")!,
            City = row.Field<string>("City")!,
            Pincode = row.Field<string>("Pincode"),
            MobileNo = row.Field<string>("MobileNo") ?? string.Empty,
            PhoneNo = row.Field<string>("PhoneNo")!,
            Email = row.Field<string>("Email")!,
            Fine = row.Field<decimal>("Fine"),
            Amount = row.Field<decimal>("Amount")
        });
    }

    public IActionResult DeleteAccount(int AccountId)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SP_Account_Delete", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@AccountId", AccountId);
            command.ExecuteNonQuery();

            TempData["SuccessMessage"] = "Account deleted successfully!";
        }
        catch (SqlException ex) when (ex.Number == 547) // FK constraint violation
        {
            TempData["ErrorMessage"] = "Cannot delete this account because it is referenced by other records.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Deletion failed: {ex.Message}";
        }
        return RedirectToAction("AccountList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveAddEditAccount(AccountModel account)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.groupList = SetGroupDropDown();
            return View("AddEditAccount", account);
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                account.AccountId == null ? "SP_Account_Insert" : "SP_Account_Update",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (account.AccountId.HasValue)
                command.Parameters.AddWithValue("@AccountId", account.AccountId);

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

            TempData["SuccessMessage"] = account.AccountId == null
                ? "Account added successfully!"
                : "Account updated successfully!";
            return RedirectToAction("AccountList");
        }
        catch (Exception ex)
        {
            ViewBag.groupList = SetGroupDropDown();
            ModelState.AddModelError("", $"Save failed: {ex.Message}");
            return View("AddEditAccount", account);
        }
    }
}