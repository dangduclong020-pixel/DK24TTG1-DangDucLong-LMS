using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Migrations
{
    /// <inheritdoc />
    public partial class AddExamDisplayOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowAnswers",
                table: "Exams",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowScore",
                table: "Exams",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSubmission",
                table: "Exams",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowAnswers",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ShowScore",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ShowSubmission",
                table: "Exams");
        }
    }
}
