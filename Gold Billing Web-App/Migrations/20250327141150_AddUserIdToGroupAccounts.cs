using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_Billing_WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToGroupAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(510)",
                maxLength: 510,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(510)",
                oldMaxLength: 510);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "GroupAccount",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GroupAccount");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(510)",
                maxLength: 510,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(510)",
                oldMaxLength: 510,
                oldNullable: true);
        }
    }
}
