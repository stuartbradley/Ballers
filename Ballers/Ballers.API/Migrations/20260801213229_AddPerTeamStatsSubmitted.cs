using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerTeamStatsSubmitted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AwayStatsSubmitted",
                table: "Fixtures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HomeStatsSubmitted",
                table: "Fixtures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing results were recorded before submissions were tracked per
            // team. Treat them as fully submitted, otherwise the next resubmission
            // on an old fixture would set IsPlayed back to false and drop a settled
            // result out of the league table.
            migrationBuilder.Sql(@"
                UPDATE Fixtures
                SET HomeStatsSubmitted = 1, AwayStatsSubmitted = 1
                WHERE IsPlayed = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayStatsSubmitted",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "HomeStatsSubmitted",
                table: "Fixtures");
        }
    }
}
