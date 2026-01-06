using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleInfoToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DayOfWeek",
                table: "Classes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "Classes",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Semester",
                table: "Classes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Session",
                table: "Classes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "Classes",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Semester",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Session",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Classes");
        }
    }
}
