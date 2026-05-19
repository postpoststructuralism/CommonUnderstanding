using CommonUnderstanding.Services;
using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add EF Core with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


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
builder.Services.AddSingleton<AiRequestTraceRecorder>();
builder.Services.AddSingleton<OpenRouterModelCatalogService>();

// Register Semantic Kernel and Belief Analysis services
builder.Services.AddSingleton<SemanticKernelService>();
builder.Services.AddScoped<BeliefAnalysisService>();

// Register Discovery services
builder.Services.AddScoped<PsychometricianAgent>();  // NEW: Expert psychometric question generation
builder.Services.AddScoped<DiscoveryQuestionEngine>();
builder.Services.AddScoped<ResponseAnalysisEngine>();
builder.Services.AddScoped<BayesianInferenceEngine>();
builder.Services.AddScoped<BeliefDiscoveryOrchestrator>();

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
    name: "default",
    pattern: "{controller=Argument}/{action=Submit}/{id?}");

// Map SignalR hub
app.MapHub<CommonUnderstanding.Hubs.DiscoveryHub>("/discoveryHub");
app.MapHub<CommonUnderstanding.Hubs.DebateHub>("/debatehub");

// Apply EF Core migrations at startup (creates tables if they don't exist)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();
