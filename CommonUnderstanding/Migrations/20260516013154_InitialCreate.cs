using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Arguments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arguments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollaborativeSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ParticipantIdsJson = table.Column<string>(type: "text", nullable: false),
                    ContributedArgumentIdsJson = table.Column<string>(type: "text", nullable: false),
                    MergedNodeIdsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcludedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JointConvergenceMapId = table.Column<int>(type: "integer", nullable: true),
                    ConsolidatedReportJson = table.Column<string>(type: "text", nullable: true),
                    ExecutiveSummary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborativeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommonUnderstandingNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "text", nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    ArgumentIdsJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tags = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommonUnderstandingNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConvergenceMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    User1Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    User2Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OverallConvergenceScore = table.Column<double>(type: "double precision", nullable: false),
                    ProfileOverlapJson = table.Column<string>(type: "text", nullable: false),
                    SharedPropositionIdsJson = table.Column<string>(type: "text", nullable: false),
                    DisputedPropositionIdsJson = table.Column<string>(type: "text", nullable: false),
                    DivergencePointsJson = table.Column<string>(type: "text", nullable: false),
                    ExpansionPathwaysJson = table.Column<string>(type: "text", nullable: false),
                    EvolutionHistoryJson = table.Column<string>(type: "text", nullable: false),
                    NarrativeSummary = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvergenceMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersistedEmergentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeepAnalysis = table.Column<bool>(type: "boolean", nullable: false),
                    TotalArguments = table.Column<int>(type: "integer", nullable: false),
                    TotalPropositions = table.Column<int>(type: "integer", nullable: false),
                    TotalEvidenceItems = table.Column<int>(type: "integer", nullable: false),
                    AverageConfidence = table.Column<double>(type: "double precision", nullable: false),
                    SettledCount = table.Column<int>(type: "integer", nullable: false),
                    ContestedCount = table.Column<int>(type: "integer", nullable: false),
                    BlindspotCount = table.Column<int>(type: "integer", nullable: false),
                    HarmonyCount = table.Column<int>(type: "integer", nullable: false),
                    CriticalAssumptionsUntested = table.Column<int>(type: "integer", nullable: false),
                    BlindspotsSummaryJson = table.Column<string>(type: "text", nullable: true),
                    HarmoniesSummaryJson = table.Column<string>(type: "text", nullable: true),
                    ExecutiveSummary = table.Column<string>(type: "text", nullable: true),
                    FullReportJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistedEmergentReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemType = table.Column<string>(type: "text", nullable: false),
                    ItemReferenceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SharedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SharedWithUserIdsJson = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReactionsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stakeholders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Organization = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stakeholders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InitiatorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InitiatorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentBeliefSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    HistoricalSnapshotsJson = table.Column<string>(type: "text", nullable: false),
                    InteractionsJson = table.Column<string>(type: "text", nullable: false),
                    AskedQuestionHashesJson = table.Column<string>(type: "text", nullable: false),
                    ExploredDimensionsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdjudicationSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArgumentId = table.Column<int>(type: "integer", nullable: false),
                    OverallConfidence = table.Column<double>(type: "double precision", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: false),
                    ReasoningTrace = table.Column<string>(type: "text", nullable: true),
                    EvidenceGapsJson = table.Column<string>(type: "text", nullable: true),
                    ConflictingEvidenceJson = table.Column<string>(type: "text", nullable: true),
                    NextSteps = table.Column<string>(type: "text", nullable: true),
                    DetailedNarrative = table.Column<string>(type: "text", nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjudicationSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdjudicationSummaries_Arguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "Arguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentComparisons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArgumentAId = table.Column<int>(type: "integer", nullable: false),
                    ArgumentBId = table.Column<int>(type: "integer", nullable: false),
                    ConflictingPremisesJson = table.Column<string>(type: "text", nullable: true),
                    ComplementaryPremisesJson = table.Column<string>(type: "text", nullable: true),
                    UniqueToPremisesAJson = table.Column<string>(type: "text", nullable: true),
                    UniqueToPremisesBJson = table.Column<string>(type: "text", nullable: true),
                    SynthesisNarrative = table.Column<string>(type: "text", nullable: true),
                    NetDirection = table.Column<string>(type: "text", nullable: false),
                    NetConfidence = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArgumentComparisons_Arguments_ArgumentAId",
                        column: x => x.ArgumentAId,
                        principalTable: "Arguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArgumentComparisons_Arguments_ArgumentBId",
                        column: x => x.ArgumentBId,
                        principalTable: "Arguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArgumentId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_Arguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "Arguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommonUnderstandingEdges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceNodeId = table.Column<int>(type: "integer", nullable: false),
                    TargetNodeId = table.Column<int>(type: "integer", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Strength = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommonUnderstandingEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommonUnderstandingEdges_CommonUnderstandingNodes_SourceNod~",
                        column: x => x.SourceNodeId,
                        principalTable: "CommonUnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommonUnderstandingEdges_CommonUnderstandingNodes_TargetNod~",
                        column: x => x.TargetNodeId,
                        principalTable: "CommonUnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StakeholderPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StakeholderId = table.Column<int>(type: "integer", nullable: false),
                    ArgumentId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AcceptedPremiseIdsJson = table.Column<string>(type: "text", nullable: false),
                    RejectedPremiseIdsJson = table.Column<string>(type: "text", nullable: false),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakeholderPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StakeholderPositions_Arguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "Arguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StakeholderPositions_Stakeholders_StakeholderId",
                        column: x => x.StakeholderId,
                        principalTable: "Stakeholders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false),
                    IsSupported = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assumptions_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Propositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    ProvisionalAssessment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProvisionalConfidence = table.Column<double>(type: "double precision", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Propositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Propositions_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Qualifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    QualifierType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Qualifiers_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rebuttals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Strength = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rebuttals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rebuttals_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Syllogisms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    MajorPremise = table.Column<string>(type: "text", nullable: false),
                    MinorPremise = table.Column<string>(type: "text", nullable: false),
                    Conclusion = table.Column<string>(type: "text", nullable: false),
                    InferenceType = table.Column<string>(type: "text", nullable: false),
                    IsValidForm = table.Column<bool>(type: "boolean", nullable: false),
                    FallaciesDetected = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Syllogisms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Syllogisms_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropositionId = table.Column<int>(type: "integer", nullable: false),
                    Citation = table.Column<string>(type: "text", nullable: false),
                    SourceUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DOI = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Tier = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    EffectSize = table.Column<double>(type: "double precision", nullable: true),
                    SampleSize = table.Column<int>(type: "integer", nullable: true),
                    ReplicationStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PublicationYear = table.Column<int>(type: "integer", nullable: true),
                    AddedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceItems_Propositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "Propositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjudicationSummaries_ArgumentId",
                table: "AdjudicationSummaries",
                column: "ArgumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentComparisons_ArgumentAId",
                table: "ArgumentComparisons",
                column: "ArgumentAId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentComparisons_ArgumentBId",
                table: "ArgumentComparisons",
                column: "ArgumentBId");

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_ClaimId",
                table: "Assumptions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ArgumentId",
                table: "Claims",
                column: "ArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommonUnderstandingEdges_SourceNodeId",
                table: "CommonUnderstandingEdges",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommonUnderstandingEdges_TargetNodeId",
                table: "CommonUnderstandingEdges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommonUnderstandingNodes_NormalizedKey",
                table: "CommonUnderstandingNodes",
                column: "NormalizedKey");

            migrationBuilder.CreateIndex(
                name: "IX_ConvergenceMaps_User1Id_User2Id",
                table: "ConvergenceMaps",
                columns: new[] { "User1Id", "User2Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_PropositionId",
                table: "EvidenceItems",
                column: "PropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Propositions_ClaimId",
                table: "Propositions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifiers_ClaimId",
                table: "Qualifiers",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Rebuttals_ClaimId",
                table: "Rebuttals",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedItems_SharedByUserId",
                table: "SharedItems",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderPositions_ArgumentId",
                table: "StakeholderPositions",
                column: "ArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderPositions_StakeholderId",
                table: "StakeholderPositions",
                column: "StakeholderId");

            migrationBuilder.CreateIndex(
                name: "IX_Syllogisms_ClaimId",
                table: "Syllogisms",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Username",
                table: "UserAccounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_InitiatorUserId_RecipientUserId",
                table: "UserConnections",
                columns: new[] { "InitiatorUserId", "RecipientUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjudicationSummaries");

            migrationBuilder.DropTable(
                name: "ArgumentComparisons");

            migrationBuilder.DropTable(
                name: "Assumptions");

            migrationBuilder.DropTable(
                name: "CollaborativeSessions");

            migrationBuilder.DropTable(
                name: "CommonUnderstandingEdges");

            migrationBuilder.DropTable(
                name: "ConvergenceMaps");

            migrationBuilder.DropTable(
                name: "EvidenceItems");

            migrationBuilder.DropTable(
                name: "PersistedEmergentReports");

            migrationBuilder.DropTable(
                name: "Qualifiers");

            migrationBuilder.DropTable(
                name: "Rebuttals");

            migrationBuilder.DropTable(
                name: "SharedItems");

            migrationBuilder.DropTable(
                name: "StakeholderPositions");

            migrationBuilder.DropTable(
                name: "Syllogisms");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "UserConnections");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "CommonUnderstandingNodes");

            migrationBuilder.DropTable(
                name: "Propositions");

            migrationBuilder.DropTable(
                name: "Stakeholders");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "Arguments");
        }
    }
}
