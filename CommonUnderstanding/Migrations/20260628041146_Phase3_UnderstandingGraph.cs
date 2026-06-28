using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_UnderstandingGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConceptualSchemas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DiscoveryMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Coherence = table.Column<double>(type: "double precision", nullable: false),
                    Stability = table.Column<double>(type: "double precision", nullable: false),
                    FactorIndex = table.Column<int>(type: "integer", nullable: true),
                    DimensionLoadingsJson = table.Column<string>(type: "text", nullable: true),
                    ArgumentLoadingsJson = table.Column<string>(type: "text", nullable: true),
                    PropositionLoadingsJson = table.Column<string>(type: "text", nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptualSchemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GraphSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    EdgeCount = table.Column<int>(type: "integer", nullable: false),
                    SchemaCount = table.Column<int>(type: "integer", nullable: false),
                    TopologySummaryJson = table.Column<string>(type: "text", nullable: false),
                    SchemaIdsJson = table.Column<string>(type: "text", nullable: false),
                    SynthesisIdsJson = table.Column<string>(type: "text", nullable: false),
                    AverageDialecticalTemperature = table.Column<double>(type: "double precision", nullable: false),
                    GraphDensity = table.Column<double>(type: "double precision", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GraphSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnderstandingNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanonicalText = table.Column<string>(type: "text", nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    SemanticEmbedding = table.Column<float[]>(type: "float4[]", nullable: true),
                    GraphEmbedding = table.Column<float[]>(type: "float4[]", nullable: true),
                    SchwartzVector = table.Column<double[]>(type: "float8[]", nullable: true),
                    MoralFoundationsVector = table.Column<double[]>(type: "float8[]", nullable: true),
                    DimensionalCoordinatesJson = table.Column<string>(type: "text", nullable: true),
                    DegreeCentrality = table.Column<double>(type: "double precision", nullable: false),
                    BetweennessCentrality = table.Column<double>(type: "double precision", nullable: false),
                    EigenvectorCentrality = table.Column<double>(type: "double precision", nullable: false),
                    PageRank = table.Column<double>(type: "double precision", nullable: false),
                    ClusteringCoefficient = table.Column<double>(type: "double precision", nullable: false),
                    ControversyScore = table.Column<double>(type: "double precision", nullable: false),
                    DialecticalTemperature = table.Column<double>(type: "double precision", nullable: false),
                    SchemaEntropy = table.Column<double>(type: "double precision", nullable: false),
                    ArgumentIdsJson = table.Column<string>(type: "text", nullable: false),
                    UserIdsJson = table.Column<string>(type: "text", nullable: false),
                    SchemaIdsJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderstandingNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialecticalSyntheses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SynthesisNodeId = table.Column<int>(type: "integer", nullable: false),
                    ParentNodeIdsJson = table.Column<string>(type: "text", nullable: false),
                    ResolvedContradictionIdsJson = table.Column<string>(type: "text", nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    ResolutionNarrative = table.Column<string>(type: "text", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialecticalSyntheses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialecticalSyntheses_UnderstandingNodes_SynthesisNodeId",
                        column: x => x.SynthesisNodeId,
                        principalTable: "UnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchemaMemberships",
                columns: table => new
                {
                    NodeId = table.Column<int>(type: "integer", nullable: false),
                    SchemaId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchemaMemberships", x => new { x.NodeId, x.SchemaId });
                    table.ForeignKey(
                        name: "FK_SchemaMemberships_ConceptualSchemas_SchemaId",
                        column: x => x.SchemaId,
                        principalTable: "ConceptualSchemas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchemaMemberships_UnderstandingNodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "UnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnderstandingEdges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceNodeId = table.Column<int>(type: "integer", nullable: false),
                    TargetNodeId = table.Column<int>(type: "integer", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    BaseWeight = table.Column<double>(type: "double precision", nullable: false),
                    ProvenanceJson = table.Column<string>(type: "text", nullable: false),
                    ReinforcementCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReinforcedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderstandingEdges", x => x.Id);
                    table.CheckConstraint("CK_UnderstandingEdges_NoSelfLoop", "\"SourceNodeId\" <> \"TargetNodeId\"");
                    table.ForeignKey(
                        name: "FK_UnderstandingEdges_UnderstandingNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "UnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnderstandingEdges_UnderstandingNodes_TargetNodeId",
                        column: x => x.TargetNodeId,
                        principalTable: "UnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptualSchemas_Coherence",
                table: "ConceptualSchemas",
                column: "Coherence");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptualSchemas_DiscoveryMethod",
                table: "ConceptualSchemas",
                column: "DiscoveryMethod");

            migrationBuilder.CreateIndex(
                name: "IX_DialecticalSyntheses_Depth",
                table: "DialecticalSyntheses",
                column: "Depth");

            migrationBuilder.CreateIndex(
                name: "IX_DialecticalSyntheses_SynthesisNodeId",
                table: "DialecticalSyntheses",
                column: "SynthesisNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_GraphSnapshots_CapturedAt",
                table: "GraphSnapshots",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SchemaMemberships_SchemaId",
                table: "SchemaMemberships",
                column: "SchemaId");

            migrationBuilder.CreateIndex(
                name: "IX_UnderstandingEdges_SourceNodeId_TargetNodeId_Relationship",
                table: "UnderstandingEdges",
                columns: new[] { "SourceNodeId", "TargetNodeId", "Relationship" });

            migrationBuilder.CreateIndex(
                name: "IX_UnderstandingEdges_TargetNodeId",
                table: "UnderstandingEdges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_UnderstandingEdges_Weight",
                table: "UnderstandingEdges",
                column: "Weight");

            migrationBuilder.CreateIndex(
                name: "IX_UnderstandingNodes_Confidence",
                table: "UnderstandingNodes",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_UnderstandingNodes_ControversyScore",
                table: "UnderstandingNodes",
                column: "ControversyScore");

            migrationBuilder.CreateIndex(
                name: "IX_UnderstandingNodes_NormalizedKey",
                table: "UnderstandingNodes",
                column: "NormalizedKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialecticalSyntheses");

            migrationBuilder.DropTable(
                name: "GraphSnapshots");

            migrationBuilder.DropTable(
                name: "SchemaMemberships");

            migrationBuilder.DropTable(
                name: "UnderstandingEdges");

            migrationBuilder.DropTable(
                name: "ConceptualSchemas");

            migrationBuilder.DropTable(
                name: "UnderstandingNodes");
        }
    }
}
