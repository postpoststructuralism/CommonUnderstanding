using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedImpressionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterestScoreAtServe = table.Column<double>(type: "float", nullable: false),
                    GrowthScoreAtServe = table.Column<double>(type: "float", nullable: false),
                    CollectiveScoreAtServe = table.Column<double>(type: "float", nullable: false),
                    BlendedScoreAtServe = table.Column<double>(type: "float", nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Clicked = table.Column<bool>(type: "bit", nullable: false),
                    Voted = table.Column<bool>(type: "bit", nullable: false),
                    Commented = table.Column<bool>(type: "bit", nullable: false),
                    DwellMs = table.Column<int>(type: "int", nullable: false),
                    ServedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedImpressionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedImpressionEvents_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFeedPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InterestWeight = table.Column<double>(type: "float", nullable: false),
                    GrowthWeight = table.Column<double>(type: "float", nullable: false),
                    CollectiveWeight = table.Column<double>(type: "float", nullable: false),
                    AdventureRate = table.Column<double>(type: "float", nullable: false),
                    ChallengeRate = table.Column<double>(type: "float", nullable: false),
                    RecencyBias = table.Column<double>(type: "float", nullable: false),
                    IncludeAIGenerated = table.Column<bool>(type: "bit", nullable: false),
                    EvidencePreferred = table.Column<bool>(type: "bit", nullable: false),
                    ActivePresetName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeedPreferences", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedImpressionEvents_ArgumentId_ServedAt",
                table: "FeedImpressionEvents",
                columns: new[] { "ArgumentId", "ServedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedImpressionEvents_UserId_ServedAt",
                table: "FeedImpressionEvents",
                columns: new[] { "UserId", "ServedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedImpressionEvents");

            migrationBuilder.DropTable(
                name: "UserFeedPreferences");
        }
    }
}
