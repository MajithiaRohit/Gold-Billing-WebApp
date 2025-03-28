using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_Billing_Web_App.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToAmountTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PaymentModes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "AmountTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PaymentModes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AmountTransactions");
        }
    }
}
