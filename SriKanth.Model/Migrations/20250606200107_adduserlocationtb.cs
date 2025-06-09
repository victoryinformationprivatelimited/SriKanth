using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SriKanth.Model.Migrations
{
    /// <inheritdoc />
    public partial class adduserlocationtb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationCode",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "UserLocation",
                columns: table => new
                {
                    UserLocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLocation", x => x.UserLocationId);
                    table.ForeignKey(
                        name: "FK_UserLocation_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserHistory_UserId",
                table: "UserHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocumentStorage_UserId",
                table: "UserDocumentStorage",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLocation_UserId",
                table: "UserLocation",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserDocumentStorage_Users_UserId",
                table: "UserDocumentStorage",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_UserHistory_Users_UserId",
                table: "UserHistory",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserDocumentStorage_Users_UserId",
                table: "UserDocumentStorage");

            migrationBuilder.DropForeignKey(
                name: "FK_UserHistory_Users_UserId",
                table: "UserHistory");

            migrationBuilder.DropTable(
                name: "UserLocation");

            migrationBuilder.DropIndex(
                name: "IX_UserHistory_UserId",
                table: "UserHistory");

            migrationBuilder.DropIndex(
                name: "IX_UserDocumentStorage_UserId",
                table: "UserDocumentStorage");

            migrationBuilder.AddColumn<string>(
                name: "LocationCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
