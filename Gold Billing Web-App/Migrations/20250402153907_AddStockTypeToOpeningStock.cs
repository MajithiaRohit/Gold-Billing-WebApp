using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_Billing_Web_App.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTypeToOpeningStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StockType",
                table: "OpeningStock",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockType",
                table: "OpeningStock");
        }
    }
}
