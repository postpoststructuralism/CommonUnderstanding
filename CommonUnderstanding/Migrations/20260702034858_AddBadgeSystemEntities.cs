using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeSystemEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DmiScore",
                table: "UserReputations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "BadgeAwardLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BadgeId = table.Column<string>(type: "text", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerSummary = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeAwardLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StructuralResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionText = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: true),
                    EndorsementCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuralResolutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResolutionEndorsements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolutionEndorsements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResolutionEndorsements_StructuralResolutions_ResolutionId",
                        column: x => x.ResolutionId,
                        principalTable: "StructuralResolutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwardLogs_BadgeId",
                table: "BadgeAwardLogs",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwardLogs_UserId",
                table: "BadgeAwardLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolutionEndorsements_ResolutionId_UserId",
                table: "ResolutionEndorsements",
                columns: new[] { "ResolutionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuralResolutions_AuthorId",
                table: "StructuralResolutions",
                column: "AuthorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BadgeAwardLogs");

            migrationBuilder.DropTable(
                name: "ResolutionEndorsements");

            migrationBuilder.DropTable(
                name: "StructuralResolutions");

            migrationBuilder.DropColumn(
                name: "DmiScore",
                table: "UserReputations");
        }
    }
}
