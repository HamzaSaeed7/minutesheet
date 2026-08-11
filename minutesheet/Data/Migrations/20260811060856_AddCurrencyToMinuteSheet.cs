using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToMinuteSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "MinuteSheets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "MinuteSheets");
        }
    }
}
