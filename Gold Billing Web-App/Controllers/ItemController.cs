using System.Data;
using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemController : Controller
    {
        private readonly IConfiguration configuration;
        public ItemController(IConfiguration _configuration)
        {
            configuration = _configuration;
        }

        // Fetch Item Group Dropdown
        public List<ItemGroupDropDownModel> SetItemGroupDropDown()
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand("SP_ItemGroupDropDown", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using SqlDataReader reader = command.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(reader);

            return dataTable.AsEnumerable().Select(row => new ItemGroupDropDownModel
            {
                Id = row.Field<int>("Id"),
                GroupName = row.Field<string>("GroupName")!
            }).ToList();
        }

        // List all items
        public IActionResult ItemList()
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand("SP_Item_SelectAll", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return View(table);
        }

        // Add/Edit Item Form
        public IActionResult AddEditItem(int? Id)
        {
            ViewBag.ItemGroupList = SetItemGroupDropDown();

            if (Id.HasValue && Id > 0)
            {
                string? connectionString = this.configuration.GetConnectionString("ConnectionString");
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                using SqlCommand command = new SqlCommand("SP_Item_SelectByID", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.AddWithValue("@Id", Id);
                using SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count > 0)
                {
                    DataRow row = table.Rows[0];
                    ItemModel item = new ItemModel
                    {
                        Id = row.Field<int>("Id"),
                        Name = row.Field<string>("Name")!,
                        ItemGroupId = row.Field<int>("ItemGroupId")
                    };
                    return View(item);
                }
            }
            return View(new ItemModel());
        }

        // Save (Insert or Update) Item
        [HttpPost]
        public IActionResult SaveItem(ItemModel item)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;

            if (item.Id == null)
            {
                command.CommandText = "SP_Item_Insert";
            }
            else
            {
                command.CommandText = "SP_Item_Update";
                command.Parameters.AddWithValue("@Id", item.Id);
            }

            command.Parameters.AddWithValue("@Name", item.Name);
            command.Parameters.AddWithValue("@ItemGroupID", item.ItemGroupId);
            command.ExecuteNonQuery();

            return RedirectToAction("ItemList");
        }

        // Delete Item
        public IActionResult DeleteItem(int Id)
        {
            string? connectionString = this.configuration.GetConnectionString("ConnectionString");
            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("Sp_Item_Delete", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Id", Id);
            connection.Open();
            command.ExecuteNonQuery();
            return RedirectToAction("ItemList");
        }
    }
}
