using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudNotes.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFavoriteToNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Notes",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Notes");
        }
    }
}
