using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SriKanth.Model.Migrations
{
    /// <inheritdoc />
    public partial class changedocumnetstore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "UserDocumentStorage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "UserDocumentStorage",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "UserDocumentStorage");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "UserDocumentStorage");
        }
    }
}
