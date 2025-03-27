using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_Billing_WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Item",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Item");
        }
    }
}
