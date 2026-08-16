using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnGoals",
                table: "FixturePlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnGoals",
                table: "FixturePlayerStats");
        }
    }
}
