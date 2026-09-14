using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddProvenanceAndSourceTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EvidenceCorpusEntryId",
                table: "EvidenceItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "EvidenceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    PublisherIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReliabilityScore = table.Column<double>(type: "float", nullable: false),
                    BiasLean = table.Column<double>(type: "float", nullable: true),
                    ExpertiseDomains = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetractionCount = table.Column<int>(type: "int", nullable: false),
                    CorrectionCount = table.Column<int>(type: "int", nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceCorpusEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Abstract = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyFindings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DOI = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Uri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublicationYear = table.Column<int>(type: "int", nullable: true),
                    SuggestedTier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceCorpusEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceCorpusEntries_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceMatchSuggestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropositionId = table.Column<int>(type: "int", nullable: false),
                    EvidenceCorpusEntryId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SimilarityScore = table.Column<double>(type: "float", nullable: false),
                    ClassificationConfidence = table.Column<double>(type: "float", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SuggestedTier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvidenceItemId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceMatchSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceMatchSuggestions_EvidenceCorpusEntries_EvidenceCorpusEntryId",
                        column: x => x.EvidenceCorpusEntryId,
                        principalTable: "EvidenceCorpusEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceMatchSuggestions_EvidenceItems_EvidenceItemId",
                        column: x => x.EvidenceItemId,
                        principalTable: "EvidenceItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvidenceMatchSuggestions_Propositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "Propositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_EvidenceCorpusEntryId",
                table: "EvidenceItems",
                column: "EvidenceCorpusEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_SourceId",
                table: "EvidenceItems",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCorpusEntries_DOI",
                table: "EvidenceCorpusEntries",
                column: "DOI");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCorpusEntries_Provider_ExternalId",
                table: "EvidenceCorpusEntries",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCorpusEntries_SourceId",
                table: "EvidenceCorpusEntries",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceMatchSuggestions_EvidenceCorpusEntryId",
                table: "EvidenceMatchSuggestions",
                column: "EvidenceCorpusEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceMatchSuggestions_EvidenceItemId",
                table: "EvidenceMatchSuggestions",
                column: "EvidenceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceMatchSuggestions_PropositionId_EvidenceCorpusEntryId",
                table: "EvidenceMatchSuggestions",
                columns: new[] { "PropositionId", "EvidenceCorpusEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Domain",
                table: "Sources",
                column: "Domain",
                unique: true,
                filter: "[Domain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_PublisherIdentifier",
                table: "Sources",
                column: "PublisherIdentifier",
                unique: true,
                filter: "[PublisherIdentifier] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceItems_EvidenceCorpusEntries_EvidenceCorpusEntryId",
                table: "EvidenceItems",
                column: "EvidenceCorpusEntryId",
                principalTable: "EvidenceCorpusEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceItems_Sources_SourceId",
                table: "EvidenceItems",
                column: "SourceId",
                principalTable: "Sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvidenceItems_EvidenceCorpusEntries_EvidenceCorpusEntryId",
                table: "EvidenceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_EvidenceItems_Sources_SourceId",
                table: "EvidenceItems");

            migrationBuilder.DropTable(
                name: "EvidenceMatchSuggestions");

            migrationBuilder.DropTable(
                name: "EvidenceCorpusEntries");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_EvidenceItems_EvidenceCorpusEntryId",
                table: "EvidenceItems");

            migrationBuilder.DropIndex(
                name: "IX_EvidenceItems_SourceId",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "EvidenceCorpusEntryId",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "EvidenceItems");
        }
    }
}
