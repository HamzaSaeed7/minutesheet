using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NEWID() gives every existing sheet a distinct token so the unique
            // index below can be created; new inserts supply their own Guid.
            migrationBuilder.AddColumn<Guid>(
                name: "Token",
                table: "MinuteSheets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActedAt",
                table: "ApprovalSteps",
                type: "datetime2",
                nullable: true);

            // Default 1 = ApprovalStepStatus.Pending for existing rows.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ApprovalSteps",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinuteSheetId = table.Column<int>(type: "int", nullable: false),
                    ApprovalStepId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_MinuteSheets_MinuteSheetId",
                        column: x => x.MinuteSheetId,
                        principalTable: "MinuteSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MinuteSheets_Token",
                table: "MinuteSheets",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_MinuteSheetId",
                table: "Comments",
                column: "MinuteSheetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_MinuteSheets_Token",
                table: "MinuteSheets");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "MinuteSheets");

            migrationBuilder.DropColumn(
                name: "ActedAt",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ApprovalSteps");
        }
    }
}
