using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTrueMotm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrueMotmPlayerId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_TrueMotmPlayerId",
                table: "Fixtures",
                column: "TrueMotmPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Players_TrueMotmPlayerId",
                table: "Fixtures",
                column: "TrueMotmPlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fixtures_Players_TrueMotmPlayerId",
                table: "Fixtures");

            migrationBuilder.DropIndex(
                name: "IX_Fixtures_TrueMotmPlayerId",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "TrueMotmPlayerId",
                table: "Fixtures");
        }
    }
}
