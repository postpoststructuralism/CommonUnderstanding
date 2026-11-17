using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateProjectReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectClaims_ReviewProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ReviewProjects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => new { x.ProjectId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProjectMembers_ReviewProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ReviewProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSourceVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Retracted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSourceVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSourceVersions_ReviewProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ReviewProjects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectClaimRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    DisplayText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchingText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attribution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objections = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PassageStart = table.Column<int>(type: "int", nullable: true),
                    PassageLength = table.Column<int>(type: "int", nullable: true),
                    PassageText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Transformation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Coverage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Lifecycle = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequiresRevalidation = table.Column<bool>(type: "bit", nullable: false),
                    FormerStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputHash = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectClaimRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectClaimRevisions_ProjectClaimRevisions_PreviousRevisionId",
                        column: x => x.PreviousRevisionId,
                        principalTable: "ProjectClaimRevisions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectClaimRevisions_ProjectClaims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "ProjectClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectClaimRevisions_ProjectSourceVersions_SourceVersionId",
                        column: x => x.SourceVersionId,
                        principalTable: "ProjectSourceVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectClaimReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ObjectionTreatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectClaimReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectClaimReviews_ProjectClaimRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "ProjectClaimRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClaimReviews_RevisionId",
                table: "ProjectClaimReviews",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClaimRevisions_ClaimId_Number",
                table: "ProjectClaimRevisions",
                columns: new[] { "ClaimId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClaimRevisions_PreviousRevisionId",
                table: "ProjectClaimRevisions",
                column: "PreviousRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClaimRevisions_SourceVersionId",
                table: "ProjectClaimRevisions",
                column: "SourceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClaims_ProjectId",
                table: "ProjectClaims",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSourceVersions_ProjectId_SourceKey_Version",
                table: "ProjectSourceVersions",
                columns: new[] { "ProjectId", "SourceKey", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectClaimReviews");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "ProjectClaimRevisions");

            migrationBuilder.DropTable(
                name: "ProjectClaims");

            migrationBuilder.DropTable(
                name: "ProjectSourceVersions");

            migrationBuilder.DropTable(
                name: "ReviewProjects");
        }
    }
}
