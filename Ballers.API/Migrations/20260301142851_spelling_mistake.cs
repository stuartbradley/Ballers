using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class spelling_mistake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fixture_Seasons_SeasonId",
                table: "Fixture");

            migrationBuilder.DropForeignKey(
                name: "FK_Fixture_Teams_AwayTeamId",
                table: "Fixture");

            migrationBuilder.DropForeignKey(
                name: "FK_Fixture_Teams_HomeTeamId",
                table: "Fixture");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Fixture",
                table: "Fixture");

            migrationBuilder.RenameTable(
                name: "Fixture",
                newName: "Fixtures");

            migrationBuilder.RenameColumn(
                name: "Kickoof",
                table: "Fixtures",
                newName: "Kickoff");

            migrationBuilder.RenameIndex(
                name: "IX_Fixture_SeasonId",
                table: "Fixtures",
                newName: "IX_Fixtures_SeasonId");

            migrationBuilder.RenameIndex(
                name: "IX_Fixture_HomeTeamId",
                table: "Fixtures",
                newName: "IX_Fixtures_HomeTeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Fixture_AwayTeamId",
                table: "Fixtures",
                newName: "IX_Fixtures_AwayTeamId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Fixtures",
                table: "Fixtures",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Seasons_SeasonId",
                table: "Fixtures",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Teams_AwayTeamId",
                table: "Fixtures",
                column: "AwayTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Teams_HomeTeamId",
                table: "Fixtures",
                column: "HomeTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fixtures_Seasons_SeasonId",
                table: "Fixtures");

            migrationBuilder.DropForeignKey(
                name: "FK_Fixtures_Teams_AwayTeamId",
                table: "Fixtures");

            migrationBuilder.DropForeignKey(
                name: "FK_Fixtures_Teams_HomeTeamId",
                table: "Fixtures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Fixtures",
                table: "Fixtures");

            migrationBuilder.RenameTable(
                name: "Fixtures",
                newName: "Fixture");

            migrationBuilder.RenameColumn(
                name: "Kickoff",
                table: "Fixture",
                newName: "Kickoof");

            migrationBuilder.RenameIndex(
                name: "IX_Fixtures_SeasonId",
                table: "Fixture",
                newName: "IX_Fixture_SeasonId");

            migrationBuilder.RenameIndex(
                name: "IX_Fixtures_HomeTeamId",
                table: "Fixture",
                newName: "IX_Fixture_HomeTeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Fixtures_AwayTeamId",
                table: "Fixture",
                newName: "IX_Fixture_AwayTeamId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Fixture",
                table: "Fixture",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixture_Seasons_SeasonId",
                table: "Fixture",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Fixture_Teams_AwayTeamId",
                table: "Fixture",
                column: "AwayTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Fixture_Teams_HomeTeamId",
                table: "Fixture",
                column: "HomeTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
