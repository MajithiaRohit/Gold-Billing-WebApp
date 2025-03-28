using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_Billing_Web_App.Migrations
{
    public partial class AddUserIdToRateCutTransactions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "RateCutTransactions",
                type: "int",
                nullable: false,
                defaultValue: 1); // Changed default to 1 (or a valid UserId)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RateCutTransactions");
        }
    }
}