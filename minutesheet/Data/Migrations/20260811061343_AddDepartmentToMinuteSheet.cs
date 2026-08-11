using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentToMinuteSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "MinuteSheets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinuteSheets_DepartmentId",
                table: "MinuteSheets",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_MinuteSheets_Departments_DepartmentId",
                table: "MinuteSheets",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MinuteSheets_Departments_DepartmentId",
                table: "MinuteSheets");

            migrationBuilder.DropIndex(
                name: "IX_MinuteSheets_DepartmentId",
                table: "MinuteSheets");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "MinuteSheets");
        }
    }
}
