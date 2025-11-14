using CommonUnderstanding.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SignalR for real-time streaming
builder.Services.AddSignalR();

// Register Semantic Kernel and Belief Analysis services
builder.Services.AddSingleton<SemanticKernelService>();
builder.Services.AddScoped<BeliefAnalysisService>();

// Register Discovery services
builder.Services.AddScoped<DiscoveryQuestionEngine>();
builder.Services.AddScoped<ResponseAnalysisEngine>();
builder.Services.AddScoped<BayesianInferenceEngine>();
builder.Services.AddScoped<BeliefDiscoveryOrchestrator>();

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
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Map SignalR hub
app.MapHub<CommonUnderstanding.Hubs.DiscoveryHub>("/discoveryHub");

app.Run();
