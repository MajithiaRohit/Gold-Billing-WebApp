﻿using System.Data;
using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemGroupController : Controller
    {
        #region Configuration
        private readonly IConfiguration configuration;
        public ItemGroupController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }
        #endregion

        #region ItemGroup List
        public IActionResult ItemGroupList()
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
                command.CommandText = "SP_ItemGroup_SelectAll";
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);
                return View(table);
            }
        }
        #endregion

        #region AddEdit ItemGroup
        public IActionResult AddEditItemGroup(int? Id)
        {
            ItemGroupModel itemGroupModel = new ItemGroupModel();

            if (Id.HasValue && Id > 0)
            {
                string? connectionString = configuration.GetConnectionString("ConnectionString");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = connection.CreateCommand();
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "SP_ItemGroup_SelectByID";
                    command.Parameters.AddWithValue("@Id", Id);
                    SqlDataReader reader = command.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);

                    if (table.Rows.Count == 0)
                    {
                        return NotFound();
                    }

                    itemGroupModel = new ItemGroupModel
                    {
                        Id = Convert.ToInt32(table.Rows[0]["Id"]),
                        Name = table.Rows[0]["Name"].ToString()!
                    };
                }
            }

            return View(itemGroupModel);
        }
        #endregion

        #region Save Method
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveItemGroup(ItemGroupModel itemGroup)
        {
            if (!ModelState.IsValid)
            {
                return View("AddEditItemGroup", itemGroup);
            }

            string? connectionString = configuration.GetConnectionString("ConnectionString");

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = connection.CreateCommand();
                    command.CommandType = CommandType.StoredProcedure;

                    if (!itemGroup.Id.HasValue || itemGroup.Id == 0)
                    {
                        command.CommandText = "SP_ItemGroup_Insert";
                    }
                    else
                    {
                        command.CommandText = "SP_ItemGroup_Update";
                        command.Parameters.AddWithValue("@Id", itemGroup.Id);
                    }

                    command.Parameters.AddWithValue("@Name", itemGroup.Name ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Date", DateTime.Now);

                    command.ExecuteNonQuery();
                }

                return RedirectToAction("ItemGroupList");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                return View("AddEditItemGroup", itemGroup);
            }
        }
        #endregion

        #region Delete
        public IActionResult DeleteItemGroup(int Id)
        {
            string? connectionString = configuration.GetConnectionString("ConnectionString");

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand("SP_ItemGroup_Delete", con);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Id", Id);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("ItemGroupList");
        }
        #endregion
    }
}