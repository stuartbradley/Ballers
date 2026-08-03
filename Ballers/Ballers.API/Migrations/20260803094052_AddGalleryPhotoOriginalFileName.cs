using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballers.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryPhotoOriginalFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "GalleryPhotos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "GalleryPhotos");
        }
    }
}
