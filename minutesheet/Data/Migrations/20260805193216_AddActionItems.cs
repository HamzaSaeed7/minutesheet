using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionItems",
                table: "MinuteSheets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionItems",
                table: "MinuteSheets");
        }
    }
}
