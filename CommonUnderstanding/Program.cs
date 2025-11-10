using CommonUnderstanding.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Discovery}/{action=Start}/{id?}")
    .WithStaticAssets();


app.Run();
