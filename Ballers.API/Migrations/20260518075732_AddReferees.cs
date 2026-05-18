using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReferees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RefereeId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Referees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_RefereeId",
                table: "Fixtures",
                column: "RefereeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Fixtures_Referees_RefereeId",
                table: "Fixtures",
                column: "RefereeId",
                principalTable: "Referees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fixtures_Referees_RefereeId",
                table: "Fixtures");

            migrationBuilder.DropTable(
                name: "Referees");

            migrationBuilder.DropIndex(
                name: "IX_Fixtures_RefereeId",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "RefereeId",
                table: "Fixtures");
        }
    }
}
