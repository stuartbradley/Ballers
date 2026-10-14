using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAccountsAndCardStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttributeCap",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoldOverallThreshold",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPointsRunAt",
                table: "LeagueSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlayerSignupEnabled",
                table: "LeagueSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerAppearance",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerAssist",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerCleanSheet",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerGoal",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerMotm",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerWin",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SignupPointsGrant",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SilverOverallThreshold",
                table: "LeagueSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerAccountRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestedTeamId = table.Column<int>(type: "int", nullable: false),
                    RequestedPlayerId = table.Column<int>(type: "int", nullable: true),
                    RequestedName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAccountRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerAccountRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerAccountRequests_Players_RequestedPlayerId",
                        column: x => x.RequestedPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlayerAccountRequests_Teams_RequestedTeamId",
                        column: x => x.RequestedTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Finishing = table.Column<int>(type: "int", nullable: false),
                    Tackling = table.Column<int>(type: "int", nullable: false),
                    Passing = table.Column<int>(type: "int", nullable: false),
                    Dribbling = table.Column<int>(type: "int", nullable: false),
                    Pace = table.Column<int>(type: "int", nullable: false),
                    Strength = table.Column<int>(type: "int", nullable: false),
                    UnspentPoints = table.Column<int>(type: "int", nullable: false),
                    TotalPointsEarned = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerCards_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerPointsAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    AppearancePoints = table.Column<int>(type: "int", nullable: false),
                    WinPoints = table.Column<int>(type: "int", nullable: false),
                    GoalPoints = table.Column<int>(type: "int", nullable: false),
                    AssistPoints = table.Column<int>(type: "int", nullable: false),
                    CleanSheetPoints = table.Column<int>(type: "int", nullable: false),
                    MotmPoints = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPointsAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPointsAwards_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerPointsAwards_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PlayerId",
                table: "AspNetUsers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAccountRequests_RequestedPlayerId",
                table: "PlayerAccountRequests",
                column: "RequestedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAccountRequests_RequestedTeamId_Status",
                table: "PlayerAccountRequests",
                columns: new[] { "RequestedTeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAccountRequests_UserId",
                table: "PlayerAccountRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCards_PlayerId",
                table: "PlayerCards",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPointsAwards_FixtureId",
                table: "PlayerPointsAwards",
                column: "FixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPointsAwards_PlayerId_FixtureId",
                table: "PlayerPointsAwards",
                columns: new[] { "PlayerId", "FixtureId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Players_PlayerId",
                table: "AspNetUsers",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // The settings row already exists in every deployed database, so the
            // new columns land on it as zeros. Backfill them with the same values
            // the entity's property initialisers give a fresh row, otherwise an
            // upgraded league starts with a points economy that pays nothing.
            migrationBuilder.Sql(@"
                UPDATE LeagueSettings SET
                    PlayerSignupEnabled    = 1,
                    SignupPointsGrant      = 80,
                    AttributeCap           = 99,
                    PointsPerAppearance    = 1,
                    PointsPerWin           = 1,
                    PointsPerGoal          = 3,
                    PointsPerAssist        = 2,
                    PointsPerCleanSheet    = 3,
                    PointsPerMotm          = 5,
                    SilverOverallThreshold = 20,
                    GoldOverallThreshold   = 35;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Players_PlayerId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "PlayerAccountRequests");

            migrationBuilder.DropTable(
                name: "PlayerCards");

            migrationBuilder.DropTable(
                name: "PlayerPointsAwards");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PlayerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AttributeCap",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "GoldOverallThreshold",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "LastPointsRunAt",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PlayerSignupEnabled",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PointsPerAppearance",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PointsPerAssist",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PointsPerCleanSheet",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PointsPerGoal",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PointsPerMotm",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PointsPerWin",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "SignupPointsGrant",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "SilverOverallThreshold",
                table: "LeagueSettings");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "AspNetUsers");
        }
    }
}
