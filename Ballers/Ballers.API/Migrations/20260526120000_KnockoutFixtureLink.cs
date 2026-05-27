using Ballers.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260526120000_KnockoutFixtureLink")]
    public partial class KnockoutFixtureLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add knockout columns to Fixtures
            migrationBuilder.AddColumn<bool>(
                name: "IsKnockout",
                table: "Fixtures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KnockoutTournament",
                table: "Fixtures",
                type: "nvarchar(max)",
                nullable: true);

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

            // Add LinkedFixtureId to KnockoutFixtures
            migrationBuilder.AddColumn<int>(
                name: "LinkedFixtureId",
                table: "KnockoutFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutFixtures_LinkedFixtureId",
                table: "KnockoutFixtures",
                column: "LinkedFixtureId");

            migrationBuilder.AddForeignKey(
                name: "FK_KnockoutFixtures_Fixtures_LinkedFixtureId",
                table: "KnockoutFixtures",
                column: "LinkedFixtureId",
                principalTable: "Fixtures",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnockoutFixtures_Fixtures_LinkedFixtureId",
                table: "KnockoutFixtures");

            migrationBuilder.DropIndex(
                name: "IX_KnockoutFixtures_LinkedFixtureId",
                table: "KnockoutFixtures");

            migrationBuilder.DropColumn(name: "LinkedFixtureId", table: "KnockoutFixtures");
            migrationBuilder.DropColumn(name: "IsKnockout", table: "Fixtures");
            migrationBuilder.DropColumn(name: "KnockoutTournament", table: "Fixtures");
            migrationBuilder.DropColumn(name: "KnockoutRound", table: "Fixtures");
            migrationBuilder.DropColumn(name: "KnockoutSlot", table: "Fixtures");
        }
    }
}
