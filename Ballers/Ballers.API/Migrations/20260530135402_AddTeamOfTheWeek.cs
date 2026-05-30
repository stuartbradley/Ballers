using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOfTheWeek : Migration
    {
        // Intentionally empty. The original generated body re-added columns
        // already created by hand-written knockout migrations. TOTW was later
        // refactored to be computed live with no persistence, so no schema
        // change is needed.

        protected override void Up(MigrationBuilder migrationBuilder) { }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
