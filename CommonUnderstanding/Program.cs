using CommonUnderstanding.Services;
using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Argument}/{action=Submit}/{id?}");

// Map SignalR hub
app.MapHub<CommonUnderstanding.Hubs.DiscoveryHub>("/discoveryHub");

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
