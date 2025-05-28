using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SriKanth.Model.Migrations
{
    /// <inheritdoc />
    public partial class changeusertable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "POSName",
                table: "Users",
                newName: "SalesPersonCode");

            migrationBuilder.AddColumn<string>(
                name: "LocationCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationCode",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "SalesPersonCode",
                table: "Users",
                newName: "POSName");
        }
    }
}
