using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace minutesheet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "DomainVocabularyTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainVocabularyTerms", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DomainVocabularyTerms",
                columns: new[] { "Id", "Aliases", "Category", "IsActive", "Term" },
                values: new object[,]
                {
                    { 1, "Ahmed, Ahmet", 0, true, "Ahmad" },
                    { 2, "Hamad", 0, true, "Hammad" },
                    { 3, "Omair, Umer", 0, true, "Umair" },
                    { 4, "Wakas, Wakkas", 0, true, "Waqas" },
                    { 5, "MinuteSheet, Minutesheet", 1, true, "Minute Sheet" },
                    { 6, "Git hub, Github", 2, true, "GitHub" },
                    { 7, "Jeera, Geera", 2, true, "Jira" },
                    { 8, "Blazer, Blazar", 3, true, "Blazor" },
                    { 9, "Dot net, dotnet", 3, true, ".NET" },
                    { 10, "Q A, cue a", 4, true, "QA" },
                    { 11, "U A T, you a t", 4, true, "UAT" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainVocabularyTerms");
        }
    }
}
