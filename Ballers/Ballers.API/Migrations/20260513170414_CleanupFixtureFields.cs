using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class CleanupFixtureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FixturePlayers_FixtureId",
                table: "FixturePlayers");

            migrationBuilder.DropColumn(
                name: "Week",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "IsStarter",
                table: "FixturePlayers");

            migrationBuilder.CreateIndex(
                name: "IX_FixturePlayers_FixtureId_PlayerId",
                table: "FixturePlayers",
                columns: new[] { "FixtureId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FixturePlayers_FixtureId_PlayerId",
                table: "FixturePlayers");

            migrationBuilder.AddColumn<int>(
                name: "Week",
                table: "Fixtures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsStarter",
                table: "FixturePlayers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_FixturePlayers_FixtureId",
                table: "FixturePlayers",
                column: "FixtureId");
        }
    }
}
