using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArgumentRecommendationFeatures",
                columns: table => new
                {
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    IsAIGenerated = table.Column<bool>(type: "bit", nullable: false),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchwartzValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasCitedEvidence = table.Column<bool>(type: "bit", nullable: false),
                    WilsonScore = table.Column<double>(type: "float", nullable: false),
                    HotScore = table.Column<double>(type: "float", nullable: false),
                    PositiveVoterBreadth = table.Column<int>(type: "int", nullable: false),
                    LinkCount = table.Column<int>(type: "int", nullable: false),
                    ContradictionCount = table.Column<int>(type: "int", nullable: false),
                    StructuralScore = table.Column<double>(type: "float", nullable: false),
                    CollectiveScore = table.Column<double>(type: "float", nullable: false),
                    FeatureVersion = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentRecommendationFeatures", x => x.ArgumentId);
                    table.ForeignKey(
                        name: "FK_ArgumentRecommendationFeatures_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentRecommendationTags",
                columns: table => new
                {
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentRecommendationTags", x => new { x.ArgumentId, x.Tag });
                    table.ForeignKey(
                        name: "FK_ArgumentRecommendationTags_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityRecommendationFeatures",
                columns: table => new
                {
                    ScopeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrendingScore = table.Column<double>(type: "float", nullable: false),
                    UnderexposedQualityScore = table.Column<double>(type: "float", nullable: false),
                    UnresolvedContradictionScore = table.Column<double>(type: "float", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityRecommendationFeatures", x => new { x.ScopeType, x.ScopeKey });
                });

            migrationBuilder.CreateTable(
                name: "RecommendationArgumentWork",
                columns: table => new
                {
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationArgumentWork", x => x.ArgumentId);
                    table.ForeignKey(
                        name: "FK_RecommendationArgumentWork_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationUserWork",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationUserWork", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserRecommendationProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SchwartzValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpertiseDomainsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HistoryWatermark = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FeatureVersion = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecommendationProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserRecommendationTopicAffinities",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Affinity = table.Column<double>(type: "float", nullable: false),
                    LastEngagedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecommendationTopicAffinities", x => new { x.UserId, x.Topic });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentRecommendationFeatures_IsEligible_CollectiveScore",
                table: "ArgumentRecommendationFeatures",
                columns: new[] { "IsEligible", "CollectiveScore" });

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentRecommendationFeatures_IsEligible_CreatedAt",
                table: "ArgumentRecommendationFeatures",
                columns: new[] { "IsEligible", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentRecommendationFeatures_IsEligible_HotScore",
                table: "ArgumentRecommendationFeatures",
                columns: new[] { "IsEligible", "HotScore" });

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentRecommendationFeatures_IsEligible_IsAIGenerated_CreatedAt",
                table: "ArgumentRecommendationFeatures",
                columns: new[] { "IsEligible", "IsAIGenerated", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentRecommendationTags_Tag_ArgumentId",
                table: "ArgumentRecommendationTags",
                columns: new[] { "Tag", "ArgumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRecommendationFeatures_ComputedAt",
                table: "CommunityRecommendationFeatures",
                column: "ComputedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationArgumentWork_RequestedAt",
                table: "RecommendationArgumentWork",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationUserWork_RequestedAt",
                table: "RecommendationUserWork",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecommendationProfiles_ComputedAt",
                table: "UserRecommendationProfiles",
                column: "ComputedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecommendationTopicAffinities_Topic_Affinity",
                table: "UserRecommendationTopicAffinities",
                columns: new[] { "Topic", "Affinity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArgumentRecommendationFeatures");

            migrationBuilder.DropTable(
                name: "ArgumentRecommendationTags");

            migrationBuilder.DropTable(
                name: "CommunityRecommendationFeatures");

            migrationBuilder.DropTable(
                name: "RecommendationArgumentWork");

            migrationBuilder.DropTable(
                name: "RecommendationUserWork");

            migrationBuilder.DropTable(
                name: "UserRecommendationProfiles");

            migrationBuilder.DropTable(
                name: "UserRecommendationTopicAffinities");
        }
    }
}
