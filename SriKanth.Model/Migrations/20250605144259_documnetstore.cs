using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SriKanth.Model.Migrations
{
    /// <inheritdoc />
    public partial class documnetstore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDocumentStorage",
                columns: table => new
                {
                    UserDocumentStorageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DocumentReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDocumentStorage", x => x.UserDocumentStorageId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDocumentStorage");
        }
    }
}
