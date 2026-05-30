using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOfTheWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsKnockoutGenerated",
                table: "Seasons",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsKnockout",
                table: "Fixtures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KnockoutRound",
                table: "Fixtures",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KnockoutSlot",
                table: "Fixtures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KnockoutTournament",
                table: "Fixtures",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KnockoutFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    Tournament = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Round = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: true),
                    AwayTeamId = table.Column<int>(type: "int", nullable: true),
                    HomeScore = table.Column<int>(type: "int", nullable: true),
                    AwayScore = table.Column<int>(type: "int", nullable: true),
                    IsPlayed = table.Column<bool>(type: "bit", nullable: false),
                    LinkedFixtureId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnockoutFixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnockoutFixtures_Fixtures_LinkedFixtureId",
                        column: x => x.LinkedFixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnockoutFixtures_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnockoutFixtures_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnockoutFixtures_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutFixtures_AwayTeamId",
                table: "KnockoutFixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutFixtures_HomeTeamId",
                table: "KnockoutFixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutFixtures_LinkedFixtureId",
                table: "KnockoutFixtures",
                column: "LinkedFixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutFixtures_SeasonId",
                table: "KnockoutFixtures",
                column: "SeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnockoutFixtures");

            migrationBuilder.DropColumn(
                name: "IsKnockoutGenerated",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "IsKnockout",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "KnockoutRound",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "KnockoutSlot",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "KnockoutTournament",
                table: "Fixtures");
        }
    }
}
