using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidentialToMinuteSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConfidential",
                table: "MinuteSheets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsConfidential",
                table: "MinuteSheets");
        }
    }
}
