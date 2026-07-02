using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddWidgetEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommentSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlanTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AllowedOrigins = table.Column<string[]>(type: "text[]", nullable: false),
                    ModerationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomCssUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThreadContradictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadIdA = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadIdB = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentIdA = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentIdB = table.Column<Guid>(type: "uuid", nullable: false),
                    ContradictionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadContradictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommentModerationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FlagReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AiConfidence = table.Column<double>(type: "double precision", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CommentSiteId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentModerationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentModerationItems_CommentSites_CommentSiteId",
                        column: x => x.CommentSiteId,
                        principalTable: "CommentSites",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommentThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThreadSlug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsModerated = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalComments = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentThreads_CommentSites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "CommentSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WidgetUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PageViews = table.Column<long>(type: "bigint", nullable: false),
                    CommentsPosted = table.Column<int>(type: "integer", nullable: false),
                    VotesCast = table.Column<int>(type: "integer", nullable: false),
                    AiAnalysesRun = table.Column<int>(type: "integer", nullable: false),
                    BandwidthBytes = table.Column<long>(type: "bigint", nullable: false),
                    CommentSiteId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WidgetUsages_CommentSites_CommentSiteId",
                        column: x => x.CommentSiteId,
                        principalTable: "CommentSites",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ThreadArguments",
                columns: table => new
                {
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsTopLevel = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadArguments", x => new { x.ThreadId, x.ArgumentId });
                    table.ForeignKey(
                        name: "FK_ThreadArguments_CommentThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "CommentThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ThreadArguments_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommentModerationItems_CommentSiteId",
                table: "CommentModerationItems",
                column: "CommentSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentModerationItems_SiteId",
                table: "CommentModerationItems",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentModerationItems_SiteId_Status",
                table: "CommentModerationItems",
                columns: new[] { "SiteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommentModerationItems_Status",
                table: "CommentModerationItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommentSites_ApiKey",
                table: "CommentSites",
                column: "ApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentSites_Domain",
                table: "CommentSites",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentSites_OwnerUserId",
                table: "CommentSites",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentThreads_SiteId_ThreadSlug",
                table: "CommentThreads",
                columns: new[] { "SiteId", "ThreadSlug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreadArguments_ArgumentId",
                table: "ThreadArguments",
                column: "ArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadContradictions_ArgumentIdA_ArgumentIdB",
                table: "ThreadContradictions",
                columns: new[] { "ArgumentIdA", "ArgumentIdB" });

            migrationBuilder.CreateIndex(
                name: "IX_ThreadContradictions_IsResolved",
                table: "ThreadContradictions",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadContradictions_SiteId",
                table: "ThreadContradictions",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_WidgetUsages_CommentSiteId",
                table: "WidgetUsages",
                column: "CommentSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_WidgetUsages_SiteId_Date",
                table: "WidgetUsages",
                columns: new[] { "SiteId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentModerationItems");

            migrationBuilder.DropTable(
                name: "ThreadArguments");

            migrationBuilder.DropTable(
                name: "ThreadContradictions");

            migrationBuilder.DropTable(
                name: "WidgetUsages");

            migrationBuilder.DropTable(
                name: "CommentThreads");

            migrationBuilder.DropTable(
                name: "CommentSites");
        }
    }
}
