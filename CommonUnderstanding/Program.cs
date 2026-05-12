using CommonUnderstanding.Services;
using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add EF Core with SQLite
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "arguments.db");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));


// Add HttpContextAccessor for views that need Request access
builder.Services.AddHttpContextAccessor();

// Add HttpClient for AI status monitoring
builder.Services.AddHttpClient();

// Add SignalR for real-time streaming
builder.Services.AddSignalR();

// Register SHARED profile store (singleton - used by both Controller and Hub)
builder.Services.AddSingleton<UserProfileStore>();

// Register Belief System Knowledge Base (singleton - loaded once at startup)
builder.Services.AddSingleton<BeliefSystemKnowledgeBase>();

// Register runtime AI config (overrides for endpoint, model, agent)
builder.Services.AddSingleton<RuntimeAiConfigService>();

// Register Semantic Kernel and Belief Analysis services
builder.Services.AddSingleton<SemanticKernelService>();
builder.Services.AddScoped<BeliefAnalysisService>();

// Register Discovery services
builder.Services.AddScoped<PsychometricianAgent>();  // NEW: Expert psychometric question generation
builder.Services.AddScoped<DiscoveryQuestionEngine>();
builder.Services.AddScoped<ResponseAnalysisEngine>();
builder.Services.AddScoped<BayesianInferenceEngine>();
builder.Services.AddScoped<BeliefDiscoveryOrchestrator>();

// Register question prefetch background service as singleton
builder.Services.AddSingleton<QuestionPrefetchService>();
// Register the singleton instance as the hosted service
builder.Services.AddHostedService(sp => sp.GetRequiredService<QuestionPrefetchService>());

// Register response processing queue background service as singleton
builder.Services.AddSingleton<ResponseProcessingQueue>();
// Register the singleton instance as the hosted service
builder.Services.AddHostedService(sp => sp.GetRequiredService<ResponseProcessingQueue>());

// Register Argument Engine services
builder.Services.AddScoped<ArgumentDecompositionService>();
builder.Services.AddScoped<LogicalValidationService>();
builder.Services.AddScoped<AdjudicationEngine>();
builder.Services.AddScoped<EvidenceClassificationService>();
builder.Services.AddScoped<CommonUnderstandingService>();
builder.Services.AddScoped<StakeholderService>();
builder.Services.AddScoped<DecisionSupportService>();
builder.Services.AddScoped<ComparativeAnalysisService>();

// Register Emergent Conclusions Engine
builder.Services.AddScoped<BlindspotDetector>();
builder.Services.AddScoped<HarmonyDetector>();
builder.Services.AddScoped<EmergentConclusionsEngine>();

// Account system (ADFS-ready cookie auth)
builder.Services.AddSingleton<AccountService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Register Multi-User Convergence services (Phase 6)
builder.Services.AddScoped<UserConnectionService>();
builder.Services.AddScoped<ConvergenceMapService>();
builder.Services.AddScoped<ConvergenceExpansionService>();
builder.Services.AddScoped<CollaborativeSessionService>();

// Add session support for user tracking
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // In development, show detailed error pages
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Argument}/{action=Submit}/{id?}");

// Map SignalR hub
app.MapHub<CommonUnderstanding.Hubs.DiscoveryHub>("/discoveryHub");
app.MapHub<CommonUnderstanding.Hubs.DebateHub>("/debatehub");

// Ensure database is created / migrations applied at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    // EnsureCreated won't add new columns to existing tables.
    // Apply lightweight schema additions for new nullable columns.
    var conn = db.Database.GetDbConnection();
    conn.Open();
    using var cmd = conn.CreateCommand();
    string[] alterStatements =
    [
        "ALTER TABLE Propositions ADD COLUMN ProvisionalAssessment TEXT NULL",
        "ALTER TABLE Propositions ADD COLUMN ProvisionalConfidence REAL NULL",
        "ALTER TABLE AdjudicationSummaries ADD COLUMN DetailedNarrative TEXT NULL",
        @"CREATE TABLE IF NOT EXISTS ArgumentComparisons (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ArgumentAId INTEGER NOT NULL REFERENCES Arguments(Id),
            ArgumentBId INTEGER NOT NULL REFERENCES Arguments(Id),
            ConflictingPremisesJson TEXT NULL,
            ComplementaryPremisesJson TEXT NULL,
            UniqueToPremisesAJson TEXT NULL,
            UniqueToPremisesBJson TEXT NULL,
            SynthesisNarrative TEXT NULL,
            NetDirection TEXT NOT NULL DEFAULT 'Insufficient',
            NetConfidence REAL NOT NULL DEFAULT 0.0,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        )",
        @"CREATE TABLE IF NOT EXISTS PersistedEmergentReports (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            GeneratedAt TEXT NOT NULL DEFAULT (datetime('now')),
            IsDeepAnalysis INTEGER NOT NULL DEFAULT 0,
            TotalArguments INTEGER NOT NULL DEFAULT 0,
            TotalPropositions INTEGER NOT NULL DEFAULT 0,
            TotalEvidenceItems INTEGER NOT NULL DEFAULT 0,
            AverageConfidence REAL NOT NULL DEFAULT 0.5,
            SettledCount INTEGER NOT NULL DEFAULT 0,
            ContestedCount INTEGER NOT NULL DEFAULT 0,
            BlindspotCount INTEGER NOT NULL DEFAULT 0,
            HarmonyCount INTEGER NOT NULL DEFAULT 0,
            CriticalAssumptionsUntested INTEGER NOT NULL DEFAULT 0,
            BlindspotsSummaryJson TEXT NULL,
            HarmoniesSummaryJson TEXT NULL,
            ExecutiveSummary TEXT NULL,
            FullReportJson TEXT NULL
        )",
        "ALTER TABLE PersistedEmergentReports ADD COLUMN FullReportJson TEXT NULL",

        // Phase 6 — Multi-User Convergence (new tables added after initial DB creation)
        @"CREATE TABLE IF NOT EXISTS UserConnections (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            InitiatorUserId TEXT NOT NULL,
            RecipientUserId TEXT NOT NULL,
            Status TEXT NOT NULL DEFAULT 'Pending',
            InitiatorMessage TEXT NULL,
            InitiatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            RespondedAt TEXT NULL
        )",
        "CREATE INDEX IF NOT EXISTS IX_UserConnections_Pair ON UserConnections (InitiatorUserId, RecipientUserId)",

        @"CREATE TABLE IF NOT EXISTS SharedItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ItemType TEXT NOT NULL,
            ItemReferenceId TEXT NOT NULL,
            ItemTitle TEXT NOT NULL DEFAULT '',
            SharedByUserId TEXT NOT NULL,
            SharedWithUserIdsJson TEXT NOT NULL DEFAULT '[]',
            Visibility TEXT NOT NULL DEFAULT 'Connections',
            Message TEXT NULL,
            SharedAt TEXT NOT NULL DEFAULT (datetime('now')),
            ReactionsJson TEXT NOT NULL DEFAULT '[]'
        )",
        "CREATE INDEX IF NOT EXISTS IX_SharedItems_SharedBy ON SharedItems (SharedByUserId)",

        @"CREATE TABLE IF NOT EXISTS ConvergenceMaps (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            User1Id TEXT NOT NULL,
            User2Id TEXT NOT NULL,
            GeneratedAt TEXT NOT NULL DEFAULT (datetime('now')),
            LastRefreshedAt TEXT NOT NULL DEFAULT (datetime('now')),
            OverallConvergenceScore REAL NOT NULL DEFAULT 0.0,
            ProfileOverlapJson TEXT NULL,
            SharedPropositionIdsJson TEXT NOT NULL DEFAULT '[]',
            DisputedPropositionIdsJson TEXT NOT NULL DEFAULT '[]',
            DivergencePointsJson TEXT NOT NULL DEFAULT '[]',
            ExpansionPathwaysJson TEXT NOT NULL DEFAULT '[]',
            EvolutionHistoryJson TEXT NOT NULL DEFAULT '[]',
            NarrativeSummary TEXT NULL
        )",
        "CREATE INDEX IF NOT EXISTS IX_ConvergenceMaps_Users ON ConvergenceMaps (User1Id, User2Id)",

        @"CREATE TABLE IF NOT EXISTS CollaborativeSessions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL DEFAULT '',
            Description TEXT NULL,
            ParticipantIdsJson TEXT NOT NULL DEFAULT '[]',
            ContributedArgumentIdsJson TEXT NOT NULL DEFAULT '[]',
            MergedNodeIdsJson TEXT NOT NULL DEFAULT '[]',
            Status TEXT NOT NULL DEFAULT 'Active',
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            ConcludedAt TEXT NULL,
            JointConvergenceMapId INTEGER NULL,
            ConsolidatedReportJson TEXT NULL,
            ExecutiveSummary TEXT NULL
        )",

        @"CREATE TABLE IF NOT EXISTS UserProfiles (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            LastInteractionAt TEXT NOT NULL DEFAULT (datetime('now')),
            Stage TEXT NOT NULL DEFAULT 'Initial',
            CurrentBeliefSnapshotJson TEXT NULL,
            HistoricalSnapshotsJson TEXT NOT NULL DEFAULT '[]',
            InteractionsJson TEXT NOT NULL DEFAULT '[]',
            AskedQuestionHashesJson TEXT NOT NULL DEFAULT '[]',
            ExploredDimensionsJson TEXT NOT NULL DEFAULT '[]'
        )",

        @"CREATE TABLE IF NOT EXISTS UserAccounts (
            Id TEXT PRIMARY KEY,
            Username TEXT NOT NULL UNIQUE,
            DisplayName TEXT NOT NULL DEFAULT '',
            PasswordHash TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            IsActive INTEGER NOT NULL DEFAULT 1
        )"
    ];
    foreach (var sql in alterStatements)
    {
        cmd.CommandText = sql;
        try { cmd.ExecuteNonQuery(); }
        catch { /* column already exists — ignore */ }
    }
    conn.Close();
}

app.Run();
