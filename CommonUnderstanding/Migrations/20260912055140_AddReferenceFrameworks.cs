using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceFrameworks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceFrameworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    JurisdictionScope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFileSize = table.Column<long>(type: "bigint", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceFrameworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceFrameworkOwners",
                columns: table => new
                {
                    ReferenceFrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceFrameworkOwners", x => new { x.ReferenceFrameworkId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ReferenceFrameworkOwners_ReferenceFrameworks_ReferenceFrameworkId",
                        column: x => x.ReferenceFrameworkId,
                        principalTable: "ReferenceFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReferenceFrameworkOwners_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferencePropositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceFrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Assessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Embedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferencePropositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferencePropositions_ReferenceFrameworks_ReferenceFrameworkId",
                        column: x => x.ReferenceFrameworkId,
                        principalTable: "ReferenceFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceFrameworkRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferencePropositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropositionId = table.Column<int>(type: "int", nullable: true),
                    SocialArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelationshipType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceFrameworkRelationships", x => x.Id);
                    table.CheckConstraint("CK_ReferenceFrameworkRelationships_ExactlyOneTarget", "([PropositionId] IS NOT NULL AND [SocialArgumentId] IS NULL) OR ([PropositionId] IS NULL AND [SocialArgumentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ReferenceFrameworkRelationships_Propositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "Propositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReferenceFrameworkRelationships_ReferencePropositions_ReferencePropositionId",
                        column: x => x.ReferencePropositionId,
                        principalTable: "ReferencePropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReferenceFrameworkRelationships_SocialArguments_SocialArgumentId",
                        column: x => x.SocialArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworkOwners_UserId",
                table: "ReferenceFrameworkOwners",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworkRelationships_PropositionId",
                table: "ReferenceFrameworkRelationships",
                column: "PropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworkRelationships_ReferencePropositionId_PropositionId",
                table: "ReferenceFrameworkRelationships",
                columns: new[] { "ReferencePropositionId", "PropositionId" },
                unique: true,
                filter: "[PropositionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworkRelationships_ReferencePropositionId_SocialArgumentId",
                table: "ReferenceFrameworkRelationships",
                columns: new[] { "ReferencePropositionId", "SocialArgumentId" },
                unique: true,
                filter: "[SocialArgumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworkRelationships_SocialArgumentId",
                table: "ReferenceFrameworkRelationships",
                column: "SocialArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworks_IsShared",
                table: "ReferenceFrameworks",
                column: "IsShared");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFrameworks_Name_Version",
                table: "ReferenceFrameworks",
                columns: new[] { "Name", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePropositions_ReferenceFrameworkId_SortOrder",
                table: "ReferencePropositions",
                columns: new[] { "ReferenceFrameworkId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceFrameworkOwners");

            migrationBuilder.DropTable(
                name: "ReferenceFrameworkRelationships");

            migrationBuilder.DropTable(
                name: "ReferencePropositions");

            migrationBuilder.DropTable(
                name: "ReferenceFrameworks");
        }
    }
}
