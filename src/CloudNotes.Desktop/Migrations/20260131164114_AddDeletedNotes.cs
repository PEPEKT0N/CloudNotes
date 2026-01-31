using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudNotes.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserEmail = table.Column<string>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_UserEmail_ServerId",
                table: "DeletedNotes",
                columns: new[] { "UserEmail", "ServerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedNotes");
        }
    }
}
