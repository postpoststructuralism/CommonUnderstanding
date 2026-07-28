using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageCounters",
                columns: table => new
                {
                    CounterKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRequestAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageCounters", x => x.CounterKey);
                });

            migrationBuilder.CreateTable(
                name: "Arguments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RawText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arguments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BadgeAwardLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BadgeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggerSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeAwardLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollaborativeSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParticipantIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContributedArgumentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MergedNodeIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcludedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JointConvergenceMapId = table.Column<int>(type: "int", nullable: true),
                    ConsolidatedReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborativeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommentSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SiteName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlanTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AllowedOrigins = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModerationMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomCssUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommonUnderstandingNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    ArgumentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommonUnderstandingNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConceptualSchemas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DiscoveryMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Coherence = table.Column<double>(type: "float", nullable: false),
                    Stability = table.Column<double>(type: "float", nullable: false),
                    FactorIndex = table.Column<int>(type: "int", nullable: true),
                    DimensionLoadingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArgumentLoadingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropositionLoadingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptualSchemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConvergenceMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    User1Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    User2Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallConvergenceScore = table.Column<double>(type: "float", nullable: false),
                    ProfileOverlapJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SharedPropositionIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisputedPropositionIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DivergencePointsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpansionPathwaysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvolutionHistoryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NarrativeSummary = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvergenceMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EpistemicProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TopicDomain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EpistemicScore = table.Column<double>(type: "float", nullable: false),
                    VoteAccuracy = table.Column<double>(type: "float", nullable: false),
                    ContributionCount = table.Column<int>(type: "int", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpistemicProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GraphSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NodeCount = table.Column<int>(type: "int", nullable: false),
                    EdgeCount = table.Column<int>(type: "int", nullable: false),
                    SchemaCount = table.Column<int>(type: "int", nullable: false),
                    TopologySummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SynthesisIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AverageDialecticalTemperature = table.Column<double>(type: "float", nullable: false),
                    GraphDensity = table.Column<double>(type: "float", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GraphSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModerationAppeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppellantUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationAppeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModerationFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlaggingUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moderators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TopicDomain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GrantedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moderators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersistedEmergentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeepAnalysis = table.Column<bool>(type: "bit", nullable: false),
                    TotalArguments = table.Column<int>(type: "int", nullable: false),
                    TotalPropositions = table.Column<int>(type: "int", nullable: false),
                    TotalEvidenceItems = table.Column<int>(type: "int", nullable: false),
                    AverageConfidence = table.Column<double>(type: "float", nullable: false),
                    SettledCount = table.Column<int>(type: "int", nullable: false),
                    ContestedCount = table.Column<int>(type: "int", nullable: false),
                    BlindspotCount = table.Column<int>(type: "int", nullable: false),
                    HarmonyCount = table.Column<int>(type: "int", nullable: false),
                    CriticalAssumptionsUntested = table.Column<int>(type: "int", nullable: false),
                    BlindspotsSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HarmoniesSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistedEmergentReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemReferenceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SharedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SharedWithUserIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReactionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialPropositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAIGenerated = table.Column<bool>(type: "bit", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Embedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPropositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stakeholders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stakeholders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StructuralResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolutionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EndorsementCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuralResolutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThreadContradictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThreadIdA = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThreadIdB = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentIdA = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentIdB = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContradictionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadContradictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnderstandingNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CanonicalText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    SemanticEmbedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GraphEmbedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SchwartzVector = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MoralFoundationsVector = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DimensionalCoordinatesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DegreeCentrality = table.Column<double>(type: "float", nullable: false),
                    BetweennessCentrality = table.Column<double>(type: "float", nullable: false),
                    EigenvectorCentrality = table.Column<double>(type: "float", nullable: false),
                    PageRank = table.Column<double>(type: "float", nullable: false),
                    ClusteringCoefficient = table.Column<double>(type: "float", nullable: false),
                    ControversyScore = table.Column<double>(type: "float", nullable: false),
                    DialecticalTemperature = table.Column<double>(type: "float", nullable: false),
                    SchemaEntropy = table.Column<double>(type: "float", nullable: false),
                    ArgumentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderstandingNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InitiatorUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitiatorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentBeliefSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HistoricalSnapshotsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteractionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AskedQuestionHashesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExploredDimensionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    XP = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Badges = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentStreak = table.Column<int>(type: "int", nullable: false),
                    LongestStreak = table.Column<int>(type: "int", nullable: false),
                    LastStreakDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StreakFreezes = table.Column<int>(type: "int", nullable: false),
                    DmiScore = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Worldviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchwartzValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchwartzVector = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worldviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XPTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XPTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdjudicationSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArgumentId = table.Column<int>(type: "int", nullable: false),
                    OverallConfidence = table.Column<double>(type: "float", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasoningTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceGapsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConflictingEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextSteps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailedNarrative = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArgumentAId = table.Column<int>(type: "int", nullable: false),
                    ArgumentBId = table.Column<int>(type: "int", nullable: false),
                    ConflictingPremisesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplementaryPremisesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UniqueToPremisesAJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UniqueToPremisesBJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SynthesisNarrative = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NetDirection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NetConfidence = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArgumentId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
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
                name: "CommentModerationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AiConfidence = table.Column<double>(type: "float", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CommentSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PageTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ThreadSlug = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsModerated = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalComments = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PageViews = table.Column<long>(type: "bigint", nullable: false),
                    CommentsPosted = table.Column<int>(type: "int", nullable: false),
                    VotesCast = table.Column<int>(type: "int", nullable: false),
                    AiAnalysesRun = table.Column<int>(type: "int", nullable: false),
                    BandwidthBytes = table.Column<long>(type: "bigint", nullable: false),
                    CommentSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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
                name: "CommonUnderstandingEdges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceNodeId = table.Column<int>(type: "int", nullable: false),
                    TargetNodeId = table.Column<int>(type: "int", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Strength = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommonUnderstandingEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommonUnderstandingEdges_CommonUnderstandingNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "CommonUnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommonUnderstandingEdges_CommonUnderstandingNodes_TargetNodeId",
                        column: x => x.TargetNodeId,
                        principalTable: "CommonUnderstandingNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DebateRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MotionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotionPropositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProponentUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpponentUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JudgeUserIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxContributionsPerSide = table.Column<int>(type: "int", nullable: false),
                    ConcludedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProponentScore = table.Column<double>(type: "float", nullable: true),
                    OpponentScore = table.Column<double>(type: "float", nullable: true),
                    AIRefereeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebateRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebateRooms_SocialPropositions_MotionPropositionId",
                        column: x => x.MotionPropositionId,
                        principalTable: "SocialPropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SocialArguments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ClaimPropositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarrantText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolutionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsShadowBanned = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceArgumentId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpvoteCount = table.Column<int>(type: "int", nullable: false),
                    DownvoteCount = table.Column<int>(type: "int", nullable: false),
                    HotScore = table.Column<double>(type: "float", nullable: false),
                    WilsonScore = table.Column<double>(type: "float", nullable: false),
                    ControversyScore = table.Column<double>(type: "float", nullable: false),
                    ReplyCount = table.Column<int>(type: "int", nullable: false),
                    IsAIValidated = table.Column<bool>(type: "bit", nullable: false),
                    AIValidityScore = table.Column<double>(type: "float", nullable: true),
                    AIFallacyFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FollowUpRelevanceScore = table.Column<double>(type: "float", nullable: true),
                    FollowUpEffectivenessNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchwartzValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialArguments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialArguments_SocialPropositions_ClaimPropositionId",
                        column: x => x.ClaimPropositionId,
                        principalTable: "SocialPropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StakeholderPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StakeholderId = table.Column<int>(type: "int", nullable: false),
                    ArgumentId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reasoning = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AcceptedPremiseIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RejectedPremiseIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "ResolutionEndorsements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "DialecticalSyntheses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SynthesisNodeId = table.Column<int>(type: "int", nullable: false),
                    ParentNodeIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolvedContradictionIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    ResolutionNarrative = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAccepted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    NodeId = table.Column<int>(type: "int", nullable: false),
                    SchemaId = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceNodeId = table.Column<int>(type: "int", nullable: false),
                    TargetNodeId = table.Column<int>(type: "int", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    BaseWeight = table.Column<double>(type: "float", nullable: false),
                    ProvenanceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReinforcementCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastReinforcedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderstandingEdges", x => x.Id);
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

            migrationBuilder.CreateTable(
                name: "WorldviewVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorldviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Vote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldviewVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldviewVotes_Worldviews_WorldviewId",
                        column: x => x.WorldviewId,
                        principalTable: "Worldviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    IsSupported = table.Column<bool>(type: "bit", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    ProvisionalAssessment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProvisionalConfidence = table.Column<double>(type: "float", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualifierType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    MajorPremise = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinorPremise = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InferenceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsValidForm = table.Column<bool>(type: "bit", nullable: false),
                    FallaciesDetected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                name: "ArgumentChains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RootArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArgumentIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentChains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArgumentChains_SocialArguments_RootArgumentId",
                        column: x => x.RootArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Annotation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArgumentLinks_SocialArguments_SourceArgumentId",
                        column: x => x.SourceArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArgumentLinks_SocialArguments_TargetArgumentId",
                        column: x => x.TargetArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Vote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EpistemicWeight = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArgumentVotes_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DebateContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DebateRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    FallacyFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidityScore = table.Column<double>(type: "float", nullable: true),
                    AIRefereeComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebateContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebateContributions_DebateRooms_DebateRoomId",
                        column: x => x.DebateRoomId,
                        principalTable: "DebateRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebateContributions_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SocialArgumentPropositions",
                columns: table => new
                {
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialArgumentPropositions", x => new { x.ArgumentId, x.PropositionId });
                    table.ForeignKey(
                        name: "FK_SocialArgumentPropositions_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialArgumentPropositions_SocialPropositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "SocialPropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThreadArguments",
                columns: table => new
                {
                    ThreadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsTopLevel = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "EvidenceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropositionId = table.Column<int>(type: "int", nullable: false),
                    Citation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DOI = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectSize = table.Column<double>(type: "float", nullable: true),
                    SampleSize = table.Column<int>(type: "int", nullable: true),
                    ReplicationStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PublicationYear = table.Column<int>(type: "int", nullable: true),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "WorldviewChains",
                columns: table => new
                {
                    WorldviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentChainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldviewChains", x => new { x.WorldviewId, x.ArgumentChainId });
                    table.ForeignKey(
                        name: "FK_WorldviewChains_ArgumentChains_ArgumentChainId",
                        column: x => x.ArgumentChainId,
                        principalTable: "ArgumentChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorldviewChains_Worldviews_WorldviewId",
                        column: x => x.WorldviewId,
                        principalTable: "Worldviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjudicationSummaries_ArgumentId",
                table: "AdjudicationSummaries",
                column: "ArgumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageCounters_LastRequestAt",
                table: "AiUsageCounters",
                column: "LastRequestAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentChains_RootArgumentId",
                table: "ArgumentChains",
                column: "RootArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentComparisons_ArgumentAId",
                table: "ArgumentComparisons",
                column: "ArgumentAId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentComparisons_ArgumentBId",
                table: "ArgumentComparisons",
                column: "ArgumentBId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentLinks_SourceArgumentId",
                table: "ArgumentLinks",
                column: "SourceArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentLinks_TargetArgumentId",
                table: "ArgumentLinks",
                column: "TargetArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentVotes_ArgumentId_UserId",
                table: "ArgumentVotes",
                columns: new[] { "ArgumentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_ClaimId",
                table: "Assumptions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwardLogs_BadgeId",
                table: "BadgeAwardLogs",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwardLogs_UserId",
                table: "BadgeAwardLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ArgumentId",
                table: "Claims",
                column: "ArgumentId");

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
                name: "IX_ConceptualSchemas_Coherence",
                table: "ConceptualSchemas",
                column: "Coherence");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptualSchemas_DiscoveryMethod",
                table: "ConceptualSchemas",
                column: "DiscoveryMethod");

            migrationBuilder.CreateIndex(
                name: "IX_ConvergenceMaps_User1Id_User2Id",
                table: "ConvergenceMaps",
                columns: new[] { "User1Id", "User2Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DebateContributions_ArgumentId",
                table: "DebateContributions",
                column: "ArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DebateContributions_DebateRoomId",
                table: "DebateContributions",
                column: "DebateRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_DebateRooms_MotionPropositionId",
                table: "DebateRooms",
                column: "MotionPropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_DialecticalSyntheses_Depth",
                table: "DialecticalSyntheses",
                column: "Depth");

            migrationBuilder.CreateIndex(
                name: "IX_DialecticalSyntheses_SynthesisNodeId",
                table: "DialecticalSyntheses",
                column: "SynthesisNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_EpistemicProfiles_UserId_TopicDomain",
                table: "EpistemicProfiles",
                columns: new[] { "UserId", "TopicDomain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_PropositionId",
                table: "EvidenceItems",
                column: "PropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_GraphSnapshots_CapturedAt",
                table: "GraphSnapshots",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAppeals_AppellantUserId",
                table: "ModerationAppeals",
                column: "AppellantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationFlags_EntityType_EntityId",
                table: "ModerationFlags",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationFlags_Status",
                table: "ModerationFlags",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Moderators_UserId_TopicDomain",
                table: "Moderators",
                columns: new[] { "UserId", "TopicDomain" });

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
                name: "IX_ResolutionEndorsements_ResolutionId_UserId",
                table: "ResolutionEndorsements",
                columns: new[] { "ResolutionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchemaMemberships_SchemaId",
                table: "SchemaMemberships",
                column: "SchemaId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedItems_SharedByUserId",
                table: "SharedItems",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialArgumentPropositions_PropositionId",
                table: "SocialArgumentPropositions",
                column: "PropositionId");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_createdat",
                table: "SocialArguments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_hotscore",
                table: "SocialArguments",
                column: "HotScore");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_sourceargumentid",
                table: "SocialArguments",
                column: "SourceArgumentId",
                unique: true,
                filter: "[SourceArgumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_wilsonscore",
                table: "SocialArguments",
                column: "WilsonScore");

            migrationBuilder.CreateIndex(
                name: "IX_SocialArguments_ClaimPropositionId",
                table: "SocialArguments",
                column: "ClaimPropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderPositions_ArgumentId",
                table: "StakeholderPositions",
                column: "ArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderPositions_StakeholderId",
                table: "StakeholderPositions",
                column: "StakeholderId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuralResolutions_AuthorId",
                table: "StructuralResolutions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Syllogisms_ClaimId",
                table: "Syllogisms",
                column: "ClaimId");

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

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Username",
                table: "UserAccounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_InitiatorUserId_RecipientUserId",
                table: "UserConnections",
                columns: new[] { "InitiatorUserId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_UserId",
                table: "UserReputations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WidgetUsages_CommentSiteId",
                table: "WidgetUsages",
                column: "CommentSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_WidgetUsages_SiteId_Date",
                table: "WidgetUsages",
                columns: new[] { "SiteId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldviewChains_ArgumentChainId",
                table: "WorldviewChains",
                column: "ArgumentChainId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldviewVotes_WorldviewId_UserId",
                table: "WorldviewVotes",
                columns: new[] { "WorldviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XPTransactions_UserId",
                table: "XPTransactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjudicationSummaries");

            migrationBuilder.DropTable(
                name: "AiUsageCounters");

            migrationBuilder.DropTable(
                name: "ArgumentComparisons");

            migrationBuilder.DropTable(
                name: "ArgumentLinks");

            migrationBuilder.DropTable(
                name: "ArgumentVotes");

            migrationBuilder.DropTable(
                name: "Assumptions");

            migrationBuilder.DropTable(
                name: "BadgeAwardLogs");

            migrationBuilder.DropTable(
                name: "CollaborativeSessions");

            migrationBuilder.DropTable(
                name: "CommentModerationItems");

            migrationBuilder.DropTable(
                name: "CommonUnderstandingEdges");

            migrationBuilder.DropTable(
                name: "ConvergenceMaps");

            migrationBuilder.DropTable(
                name: "DebateContributions");

            migrationBuilder.DropTable(
                name: "DialecticalSyntheses");

            migrationBuilder.DropTable(
                name: "EpistemicProfiles");

            migrationBuilder.DropTable(
                name: "EvidenceItems");

            migrationBuilder.DropTable(
                name: "GraphSnapshots");

            migrationBuilder.DropTable(
                name: "ModerationAppeals");

            migrationBuilder.DropTable(
                name: "ModerationFlags");

            migrationBuilder.DropTable(
                name: "Moderators");

            migrationBuilder.DropTable(
                name: "PersistedEmergentReports");

            migrationBuilder.DropTable(
                name: "Qualifiers");

            migrationBuilder.DropTable(
                name: "Rebuttals");

            migrationBuilder.DropTable(
                name: "ResolutionEndorsements");

            migrationBuilder.DropTable(
                name: "SchemaMemberships");

            migrationBuilder.DropTable(
                name: "SharedItems");

            migrationBuilder.DropTable(
                name: "SocialArgumentPropositions");

            migrationBuilder.DropTable(
                name: "StakeholderPositions");

            migrationBuilder.DropTable(
                name: "Syllogisms");

            migrationBuilder.DropTable(
                name: "ThreadArguments");

            migrationBuilder.DropTable(
                name: "ThreadContradictions");

            migrationBuilder.DropTable(
                name: "UnderstandingEdges");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "UserConnections");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "UserReputations");

            migrationBuilder.DropTable(
                name: "WidgetUsages");

            migrationBuilder.DropTable(
                name: "WorldviewChains");

            migrationBuilder.DropTable(
                name: "WorldviewVotes");

            migrationBuilder.DropTable(
                name: "XPTransactions");

            migrationBuilder.DropTable(
                name: "CommonUnderstandingNodes");

            migrationBuilder.DropTable(
                name: "DebateRooms");

            migrationBuilder.DropTable(
                name: "Propositions");

            migrationBuilder.DropTable(
                name: "StructuralResolutions");

            migrationBuilder.DropTable(
                name: "ConceptualSchemas");

            migrationBuilder.DropTable(
                name: "Stakeholders");

            migrationBuilder.DropTable(
                name: "CommentThreads");

            migrationBuilder.DropTable(
                name: "UnderstandingNodes");

            migrationBuilder.DropTable(
                name: "ArgumentChains");

            migrationBuilder.DropTable(
                name: "Worldviews");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "CommentSites");

            migrationBuilder.DropTable(
                name: "SocialArguments");

            migrationBuilder.DropTable(
                name: "Arguments");

            migrationBuilder.DropTable(
                name: "SocialPropositions");
        }
    }
}
