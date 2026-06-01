using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageCounters",
                columns: table => new
                {
                    CounterKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRequestAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageCounters", x => x.CounterKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageCounters_LastRequestAt",
                table: "AiUsageCounters",
                column: "LastRequestAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageCounters");
        }
    }
}
