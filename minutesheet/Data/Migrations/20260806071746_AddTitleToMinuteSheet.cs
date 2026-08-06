using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleToMinuteSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "MinuteSheets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Give pre-existing sheets a sensible title so they aren't blank.
            migrationBuilder.Sql(
                "UPDATE [MinuteSheets] SET [Title] = " +
                "CASE WHEN [Category] = 2 THEN 'Non-Financial minute sheet' ELSE 'Financial minute sheet' END " +
                "WHERE [Title] = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "MinuteSheets");
        }
    }
}
