using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetSharingAndSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SheetShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinuteSheetId = table.Column<int>(type: "int", nullable: false),
                    SharedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SharedWithEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetShares_AspNetUsers_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SheetShares_MinuteSheets_MinuteSheetId",
                        column: x => x.MinuteSheetId,
                        principalTable: "MinuteSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SheetSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinuteSheetId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsAllOk = table.Column<bool>(type: "bit", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetSuggestions_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SheetSuggestions_MinuteSheets_MinuteSheetId",
                        column: x => x.MinuteSheetId,
                        principalTable: "MinuteSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SheetShares_MinuteSheetId",
                table: "SheetShares",
                column: "MinuteSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_SheetShares_SharedByUserId",
                table: "SheetShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SheetSuggestions_AuthorUserId",
                table: "SheetSuggestions",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SheetSuggestions_MinuteSheetId",
                table: "SheetSuggestions",
                column: "MinuteSheetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SheetShares");

            migrationBuilder.DropTable(
                name: "SheetSuggestions");
        }
    }
}
