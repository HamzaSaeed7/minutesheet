using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIntendedForDepartmentToMinuteSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntendedForDepartmentId",
                table: "MinuteSheets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinuteSheets_IntendedForDepartmentId",
                table: "MinuteSheets",
                column: "IntendedForDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_MinuteSheets_Departments_IntendedForDepartmentId",
                table: "MinuteSheets",
                column: "IntendedForDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MinuteSheets_Departments_IntendedForDepartmentId",
                table: "MinuteSheets");

            migrationBuilder.DropIndex(
                name: "IX_MinuteSheets_IntendedForDepartmentId",
                table: "MinuteSheets");

            migrationBuilder.DropColumn(
                name: "IntendedForDepartmentId",
                table: "MinuteSheets");
        }
    }
}
