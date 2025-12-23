using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudNotes.Migrations
{
    public partial class AddSyncFieldsToNote : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSynced",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "Notes",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSynced",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Notes");
        }
    }
}

