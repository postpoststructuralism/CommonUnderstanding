using Azure.Core;
using Azure.Identity;
using CommonUnderstanding.Admin.Options;
using CommonUnderstanding.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5198");
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.Configure<AzureResourceOptions>(builder.Configuration.GetSection("Azure"));
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));
builder.Services.AddSingleton<TokenCredential>(serviceProvider =>
{
    var resources = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureResourceOptions>>();
    return new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        TenantId = resources.Value.TenantId,
        ExcludeInteractiveBrowserCredential = false
    });
});
builder.Services.AddHttpClient<AzureMonitorService>();
builder.Services.AddHttpClient<EndpointTrafficService>();
builder.Services.AddHttpClient<AvailabilityMonitorService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<AdoptionMetricsService>();
builder.Services.AddSingleton<DashboardService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The admin console is local-only and must run in Development.");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

public partial class Program;