using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudNotes.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserEmail = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CardsStudied = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyActivities_UserEmail_Date",
                table: "StudyActivities",
                columns: new[] { "UserEmail", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyActivities");
        }
    }
}
