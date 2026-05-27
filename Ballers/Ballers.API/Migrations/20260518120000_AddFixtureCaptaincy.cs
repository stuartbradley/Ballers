using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFixtureCaptaincy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayCaptainId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayViceCaptainId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeCaptainId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeViceCaptainId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_AwayCaptainId",
                table: "Fixtures",
                column: "AwayCaptainId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_AwayViceCaptainId",
                table: "Fixtures",
                column: "AwayViceCaptainId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_HomeCaptainId",
                table: "Fixtures",
                column: "HomeCaptainId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_HomeViceCaptainId",
                table: "Fixtures",
                column: "HomeViceCaptainId");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Players_AwayCaptainId",
                table: "Fixtures",
                column: "AwayCaptainId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Players_AwayViceCaptainId",
                table: "Fixtures",
                column: "AwayViceCaptainId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Players_HomeCaptainId",
                table: "Fixtures",
                column: "HomeCaptainId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Players_HomeViceCaptainId",
                table: "Fixtures",
                column: "HomeViceCaptainId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_Fixtures_Players_AwayCaptainId", "Fixtures");
            migrationBuilder.DropForeignKey("FK_Fixtures_Players_AwayViceCaptainId", "Fixtures");
            migrationBuilder.DropForeignKey("FK_Fixtures_Players_HomeCaptainId", "Fixtures");
            migrationBuilder.DropForeignKey("FK_Fixtures_Players_HomeViceCaptainId", "Fixtures");

            migrationBuilder.DropIndex("IX_Fixtures_AwayCaptainId", "Fixtures");
            migrationBuilder.DropIndex("IX_Fixtures_AwayViceCaptainId", "Fixtures");
            migrationBuilder.DropIndex("IX_Fixtures_HomeCaptainId", "Fixtures");
            migrationBuilder.DropIndex("IX_Fixtures_HomeViceCaptainId", "Fixtures");

            migrationBuilder.DropColumn("AwayCaptainId", "Fixtures");
            migrationBuilder.DropColumn("AwayViceCaptainId", "Fixtures");
            migrationBuilder.DropColumn("HomeCaptainId", "Fixtures");
            migrationBuilder.DropColumn("HomeViceCaptainId", "Fixtures");
        }
    }
}
