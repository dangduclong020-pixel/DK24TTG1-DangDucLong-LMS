using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonIdToAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "Assignments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_LessonId",
                table: "Assignments",
                column: "LessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Lessons_LessonId",
                table: "Assignments",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "LessonID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Lessons_LessonId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_LessonId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "Assignments");
        }
    }
}
