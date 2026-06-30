using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Background hosted service that periodically runs schema discovery
/// on the Understanding Graph. Discovers emergent conceptual schemas
/// using k-means clustering on semantic embeddings.
/// </summary>
public class SchemaDiscoveryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchemaDiscoveryWorker> _logger;
    private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

    public SchemaDiscoveryWorker(
        IServiceProvider serviceProvider,
        ILogger<SchemaDiscoveryWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SchemaDiscoveryWorker started. Next run in {Delay}.", _initialDelay);

        // Short initial delay so the app can warm up
        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDiscoveryAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schema discovery cycle failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunDiscoveryAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting schema discovery cycle...");

        using var scope = _serviceProvider.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<SchemaDiscoveryService>();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        // Check if there are nodes with embeddings to cluster
        await using var context = await db.CreateDbContextAsync(ct);
        var nodeCount = await context.UnderstandingNodes.CountAsync(n => n.SemanticEmbedding != null, ct);
        if (nodeCount < 3)
        {
            _logger.LogInformation("Skipping discovery: only {Count} nodes with embeddings.", nodeCount);
            return;
        }

        // Run k-means discovery
        var schemas = await discovery.DiscoverSchemasKMeansAsync();

        _logger.LogInformation(
            "Schema discovery cycle complete: {Count} schemas discovered.",
            schemas.Count);
    }
}