using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyShootout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PenaltyShootouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyShootouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PenaltyShootouts_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PenaltyKicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShootoutId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Scored = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyKicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PenaltyKicks_PenaltyShootouts_ShootoutId",
                        column: x => x.ShootoutId,
                        principalTable: "PenaltyShootouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PenaltyKicks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PenaltyKicks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyKicks_PlayerId",
                table: "PenaltyKicks",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyKicks_ShootoutId",
                table: "PenaltyKicks",
                column: "ShootoutId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyKicks_TeamId",
                table: "PenaltyKicks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyShootouts_FixtureId",
                table: "PenaltyShootouts",
                column: "FixtureId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PenaltyKicks");

            migrationBuilder.DropTable(
                name: "PenaltyShootouts");
        }
    }
}
