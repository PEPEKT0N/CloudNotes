using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudNotes.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailToNotesAndFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserEmail",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserEmail",
                table: "Folders",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserEmail",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UserEmail",
                table: "Folders");
        }
    }
}
