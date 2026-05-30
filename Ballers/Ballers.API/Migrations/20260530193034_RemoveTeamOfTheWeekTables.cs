using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTeamOfTheWeekTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamOfTheWeeks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    MatchNumber = table.Column<int>(type: "int", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamOfTheWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamOfTheWeeks_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamOfTheWeekPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamOfTheWeekId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    IsGoalkeeper = table.Column<bool>(type: "bit", nullable: false),
                    Goals = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    GoalsConceded = table.Column<int>(type: "int", nullable: true),
                    CleanSheets = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamOfTheWeekPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamOfTheWeekPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamOfTheWeekPlayers_TeamOfTheWeeks_TeamOfTheWeekId",
                        column: x => x.TeamOfTheWeekId,
                        principalTable: "TeamOfTheWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOfTheWeekPlayers_PlayerId",
                table: "TeamOfTheWeekPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamOfTheWeekPlayers_TeamOfTheWeekId",
                table: "TeamOfTheWeekPlayers",
                column: "TeamOfTheWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamOfTheWeeks_SeasonId_MatchNumber",
                table: "TeamOfTheWeeks",
                columns: new[] { "SeasonId", "MatchNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamOfTheWeekPlayers");

            migrationBuilder.DropTable(
                name: "TeamOfTheWeeks");
        }
    }
}
