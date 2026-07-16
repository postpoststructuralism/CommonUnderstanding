using CommonUnderstanding.Services;
using CommonUnderstanding.Services.Social;
using CommonUnderstanding.Services.Social.Plugins;
using CommonUnderstanding.Services.Social.Workers;
using CommonUnderstanding.Services.Widget;
using CommonUnderstanding.Data;
using CommonUnderstanding.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();

// Add EF Core with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// EF Core DbContext factory for use in SignalR hubs and background workers
// Must be Scoped so it can consume the Scoped DbContextOptions registered by AddDbContext
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// Singleton-safe wrapper for workers and hubhttps://localhost:44347/#s that can't consume Scoped services
builder.Services.AddSingleton<SingletonDbContextFactory>();


// Add HttpContextAccessor for views that need Request access
builder.Services.AddHttpContextAccessor();

// Add response compression for API endpoints (reduces JSON payload size)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = new[] { "application/json", "text/json", "application/javascript", "text/css" };
});

// Add output caching for API endpoints (reduces repeated DB queries)
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(30)));
});

// Add HttpClient for AI status monitoring
builder.Services.AddHttpClient();

// Add SignalR for real-time streaming
builder.Services.AddSignalR();

// Register SHARED profile store (singleton - used by both Controller and Hub)
builder.Services.AddSingleton<UserProfileStore>();

// Register Belief System Knowledge Base (singleton - loaded once at startup)
builder.Services.AddSingleton<BeliefSystemKnowledgeBase>();

// Register AI-powered worldview summary generator
builder.Services.AddScoped<WorldviewSummaryService>();

// Register runtime AI config (overrides for endpoint, model, agent)
builder.Services.AddSingleton<RuntimeAiConfigService>();
builder.Services.AddSingleton<AiRequestTraceRecorder>();

// Register Semantic Kernel and Belief Analysis services
builder.Services.AddSingleton<SemanticKernelService>();

// Expose the kernel's IChatCompletionService to the app DI so other services
// (e.g. WorldviewSummaryService) can inject it directly.
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var kernelService = sp.GetRequiredService<SemanticKernelService>();
    return kernelService.GetKernel().GetRequiredService<IChatCompletionService>();
});

builder.Services.AddScoped<BeliefAnalysisService>();

// Register Discovery services
builder.Services.AddScoped<PsychometricianAgent>();  // NEW: Expert psychometric question generation
builder.Services.AddScoped<DiscoveryQuestionEngine>();
builder.Services.AddScoped<ResponseAnalysisEngine>();
builder.Services.AddScoped<BayesianInferenceEngine>();
builder.Services.AddScoped<BeliefDiscoveryOrchestrator>();
builder.Services.AddSingleton<PersonalityInsightGenerator>();

// QuestionPrefetchService is on-demand only — called explicitly during Discovery.
// It is NOT registered as a hosted service so it does not poll in the background.
builder.Services.AddSingleton<QuestionPrefetchService>();

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
        // Return 401 for API routes instead of redirecting to login
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

// ── Phase 3: Understanding Graph Services ────────────────────────────────────
builder.Services.AddScoped<UnderstandingGraphService>();
builder.Services.AddScoped<SchemaDiscoveryService>();
builder.Services.AddScoped<DialecticalSynthesisService>();

// Phase 3b: Graph Algorithms — Tensor decomposition, FCA, TDA, snapshots, query
builder.Services.AddScoped<TensorConstructionService>();
builder.Services.AddScoped<TensorDecompositionService>();
builder.Services.AddScoped<FcaLatticeService>();
builder.Services.AddScoped<TdaService>();
builder.Services.AddScoped<GraphSnapshotService>();
builder.Services.AddScoped<UnderstandingQueryService>();

// Phase 3c: Background schema discovery worker
builder.Services.AddHostedService<SchemaDiscoveryWorker>();

// Register Multi-User Convergence services (Phase 6)
builder.Services.AddScoped<UserConnectionService>();
builder.Services.AddScoped<ConvergenceMapService>();
builder.Services.AddScoped<ConvergenceExpansionService>();
builder.Services.AddScoped<CollaborativeSessionService>();

// ── Phase 2: Social Platform Services ────────────────────────────────────────

// Core scoring and reputation
builder.Services.AddScoped<EpistemicScoringService>();
builder.Services.AddScoped<BadgeAwardService>();
builder.Services.AddScoped<XPAwardService>();
builder.Services.AddScoped<DmiScoreService>();
builder.Services.AddScoped<ResolutionEndorsementService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddSingleton<LocalEmbeddingGenerator>();
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    sp => sp.GetRequiredService<LocalEmbeddingGenerator>());
builder.Services.AddScoped<FeedService>();

// Voting
builder.Services.AddScoped<VotingService>();

// Follow-up arguments (replies)
builder.Services.AddScoped<FollowUpArgumentService>();
builder.Services.AddScoped<ArgumentValidationService>();
builder.Services.AddScoped<SocialArgumentAnalysisService>();

// Argument chain and worldview logic
builder.Services.AddScoped<ArgumentChainService>();

// AI Plugins
builder.Services.AddScoped<FallacyDetectionPlugin>();
builder.Services.AddScoped<ArgumentLinkSuggestionPlugin>();
builder.Services.AddScoped<WorldviewConvergencePlugin>();
builder.Services.AddScoped<BridgeArgumentPlugin>();

// WorldviewService depends on WorldviewConvergencePlugin — register after the plugin
builder.Services.AddScoped<WorldviewService>();

// Background workers
builder.Services.AddHostedService<HotScoreUpdateWorker>();
builder.Services.AddHostedService<EpistemicScoringWorker>();
builder.Services.AddHostedService<AIValidationWorker>();
builder.Services.AddHostedService<EmbeddingBackfillWorker>();
builder.Services.AddHostedService<ReplyCountWorker>();
builder.Services.AddHostedService<DmiScoreWorker>();

// ── Widget / Embeddable Comments Services ────────────────────────────────────
builder.Services.AddScoped<ThreadService>();
builder.Services.AddScoped<WidgetModerationService>();
builder.Services.AddScoped<WidgetAnalyticsService>();
builder.Services.AddHostedService<CrossThreadContradictionWorker>();

// API Key authentication for widget endpoints
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme, null);

// Add session support for user tracking
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Add authentication middleware
app.UseAuthentication();

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
app.UseResponseCompression();
app.UseOutputCache();
app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    var traceRecorder = context.RequestServices.GetRequiredService<AiRequestTraceRecorder>();
    context.Response.OnStarting(() =>
    {
        traceRecorder.WriteResponseHeaders(context);
        return Task.CompletedTask;
    });

    await next();
});

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "understandingGraph",
    pattern: "UnderstandingGraph/{action=Index}/{id?}",
    defaults: new { controller = "UnderstandingGraph" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=SocialView}/{action=Feed}/{id?}");

// Map SignalR hub
app.MapHub<CommonUnderstanding.Hubs.DiscoveryHub>("/discoveryHub");
app.MapHub<CommonUnderstanding.Hubs.DebateHub>("/debatehub");

// Phase 2 SignalR hubs
app.MapHub<CommonUnderstanding.Hubs.VotingHub>("/hubs/voting");
app.MapHub<CommonUnderstanding.Hubs.Phase2DebateHub>("/hubs/debate");
app.MapHub<CommonUnderstanding.Hubs.ChainUpdateHub>("/hubs/chains");
app.MapHub<CommonUnderstanding.Hubs.ReputationHub>("/hubs/reputation");

// Widget SignalR hub
app.MapHub<CommonUnderstanding.Hubs.WidgetHub>("/hubs/widget");

// Apply EF Core migrations at startup (creates tables if they don't exist)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Phase 1 (4.6): Only seed sample data when explicitly opted in.
    // In development, set SEED_SAMPLE_DATA=true environment variable or pass --seed flag.
    var shouldSeed = app.Environment.IsDevelopment() &&
        (args.Contains("--seed") ||
         Environment.GetEnvironmentVariable("SEED_SAMPLE_DATA")?.ToLowerInvariant() == "true");
    if (shouldSeed)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await Phase2SeedData.SeedAllAsync(db, logger);
    }
}

app.Run();
