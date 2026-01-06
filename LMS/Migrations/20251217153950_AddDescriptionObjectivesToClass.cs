using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionObjectivesToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if AdministrativeClassId column exists before adding
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'AdministrativeClassId')
                BEGIN
                    ALTER TABLE [Users] ADD [AdministrativeClassId] int NULL;
                END
            ");

            // Check if Description column exists before adding
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Classes]') AND name = 'Description')
                BEGIN
                    ALTER TABLE [Classes] ADD [Description] nvarchar(max) NULL;
                END
            ");

            // Check if Objectives column exists before adding
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Classes]') AND name = 'Objectives')
                BEGIN
                    ALTER TABLE [Classes] ADD [Objectives] nvarchar(max) NULL;
                END
            ");

            migrationBuilder.CreateTable(
                name: "AdministrativeClasses",
                columns: table => new
                {
                    AdministrativeClassID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FacultyID = table.Column<int>(type: "int", nullable: false),
                    DepartmentID = table.Column<int>(type: "int", nullable: true),
                    AcademicYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Intake = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaxStudents = table.Column<int>(type: "int", nullable: true, defaultValue: 40),
                    CurrentStudents = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    AdvisorID = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Administ__ClassID", x => x.AdministrativeClassID);
                    table.ForeignKey(
                        name: "FK__AdmClass__Advisor",
                        column: x => x.AdvisorID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK__AdmClass__Department",
                        column: x => x.DepartmentID,
                        principalTable: "Departments",
                        principalColumn: "DepartmentID");
                    table.ForeignKey(
                        name: "FK__AdmClass__Faculty",
                        column: x => x.FacultyID,
                        principalTable: "Faculties",
                        principalColumn: "FacultyID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_AdministrativeClassId",
                table: "Users",
                column: "AdministrativeClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeClasses_AdvisorID",
                table: "AdministrativeClasses",
                column: "AdvisorID");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeClasses_DepartmentID",
                table: "AdministrativeClasses",
                column: "DepartmentID");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeClasses_FacultyID",
                table: "AdministrativeClasses",
                column: "FacultyID");

            migrationBuilder.CreateIndex(
                name: "UQ__AdmClass__Code",
                table: "AdministrativeClasses",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK__Users__AdmClass",
                table: "Users",
                column: "AdministrativeClassId",
                principalTable: "AdministrativeClasses",
                principalColumn: "AdministrativeClassID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Users__AdmClass",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AdministrativeClasses");

            migrationBuilder.DropIndex(
                name: "IX_Users_AdministrativeClassId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AdministrativeClassId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Objectives",
                table: "Classes");
        }
    }
}
