using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFixturePostcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Postcode",
                table: "Fixtures",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Postcode",
                table: "Fixtures");
        }
    }
}
