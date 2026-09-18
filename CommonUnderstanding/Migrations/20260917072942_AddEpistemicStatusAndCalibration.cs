using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddEpistemicStatusAndCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EpistemicStatus",
                table: "Propositions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropositionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    WorldviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Probability = table.Column<double>(type: "float", nullable: false),
                    ResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualOutcome = table.Column<bool>(type: "bit", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.CheckConstraint("CK_Predictions_Probability", "[Probability] >= 0 AND [Probability] <= 1");
                    table.ForeignKey(
                        name: "FK_Predictions_Propositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "Propositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Predictions_Worldviews_WorldviewId",
                        column: x => x.WorldviewId,
                        principalTable: "Worldviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_PropositionId_ResolutionDate",
                table: "Predictions",
                columns: new[] { "PropositionId", "ResolutionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId_ResolvedAt",
                table: "Predictions",
                columns: new[] { "UserId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_WorldviewId_ResolvedAt",
                table: "Predictions",
                columns: new[] { "WorldviewId", "ResolvedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropColumn(
                name: "EpistemicStatus",
                table: "Propositions");
        }
    }
}
