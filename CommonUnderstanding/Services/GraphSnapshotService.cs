using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Captures point-in-time snapshots of the Understanding Graph's topological
/// structure. Enables tracking schema evolution, dialectical temperature
/// changes, and overall graph health over time.
///
/// Each snapshot records:
/// - Node/edge/schema counts
/// - Average topology metrics (centrality, clustering, dialectical temperature)
/// - Schema inventory with coherence scores
/// - Delta from previous snapshot (growth, shrinkage, merges, splits)
/// </summary>
public class GraphSnapshotService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly UnderstandingGraphService _graphService;
    private readonly SchemaDiscoveryService _schemaService;
    private readonly ILogger<GraphSnapshotService> _logger;

    public GraphSnapshotService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        UnderstandingGraphService graphService,
        SchemaDiscoveryService schemaService,
        ILogger<GraphSnapshotService> logger)
    {
        _contextFactory = contextFactory;
        _graphService = graphService;
        _schemaService = schemaService;
        _logger = logger;
    }

    // ── Snapshot Capture ──────────────────────────────────────────────────

    /// <summary>
    /// Captures a full snapshot of the current Understanding Graph state.
    /// </summary>
    public async Task<GraphSnapshot> CaptureSnapshotAsync(string? label = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Use COUNT and AVG queries instead of loading all entities.
        // This avoids transferring heavy embedding columns (SemanticEmbedding ~6KB/row,
        // GraphEmbedding, SchwartzVector, MoralFoundationsVector) over the network.
        var nodeCount = await db.UnderstandingNodes.CountAsync();
        var edgeCount = await db.UnderstandingEdges.CountAsync();
        var schemaCount = await db.ConceptualSchemas.CountAsync();

        // Aggregate stats via SQL — much cheaper than loading all rows
        var stats = await db.UnderstandingNodes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgConfidence = g.Average(n => n.Confidence),
                AvgDegreeCentrality = g.Average(n => n.DegreeCentrality),
                AvgBetweennessCentrality = g.Average(n => n.BetweennessCentrality),
                AvgClusteringCoefficient = g.Average(n => n.ClusteringCoefficient),
                AvgDialecticalTemperature = g.Average(n => n.DialecticalTemperature),
                AvgControversyScore = g.Average(n => n.ControversyScore),
                AvgSchemaEntropy = g.Average(n => n.SchemaEntropy),
                TotalEvidenceCount = g.Sum(n => n.EvidenceCount),
                SettledCount = g.Count(n => n.Status == PropositionStatus.Settled),
                ContestedCount = g.Count(n => n.Status == PropositionStatus.Contested),
                UnknownCount = g.Count(n => n.Status == PropositionStatus.Unknown),
                UnevaluatedCount = g.Count(n => n.Status == PropositionStatus.Unevaluated)
            })
            .FirstOrDefaultAsync();

        double avgConfidence = stats?.AvgConfidence ?? 0;
        double avgCentrality = stats?.AvgDegreeCentrality ?? 0;
        double avgBetweenness = stats?.AvgBetweennessCentrality ?? 0;
        double avgClustering = stats?.AvgClusteringCoefficient ?? 0;
        double avgDialecticalTemp = stats?.AvgDialecticalTemperature ?? 0;
        double avgControversy = stats?.AvgControversyScore ?? 0;
        double avgSchemaEntropy = stats?.AvgSchemaEntropy ?? 0;
        int totalEvidenceCount = stats?.TotalEvidenceCount ?? 0;
        int settledCount = stats?.SettledCount ?? 0;
        int contestedCount = stats?.ContestedCount ?? 0;
        int unknownCount = stats?.UnknownCount ?? 0;
        int unevaluatedCount = stats?.UnevaluatedCount ?? 0;

        int possibleEdges = nodeCount * (nodeCount - 1) / 2;
        double density = possibleEdges > 0 ? (double)edgeCount / possibleEdges : 0;

        // Schema inventory — only load IDs (lightweight)
        var schemaIds = await db.ConceptualSchemas
            .Select(s => s.Id)
            .ToListAsync();

        var synthesisIds = await db.DialecticalSyntheses
            .Select(ds => ds.Id)
            .ToListAsync();

        _logger.LogInformation("Capturing graph snapshot (projection-based): {N} nodes, {E} edges, {S} schemas.",
            nodeCount, edgeCount, schemaCount);

        // Compute delta from previous snapshot
        var previous = await db.GraphSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync();

        var snapshot = new GraphSnapshot
        {
            Label = label ?? $"Snapshot {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            NodeCount = nodeCount,
            EdgeCount = edgeCount,
            SchemaCount = schemaCount,
            TopologySummaryJson = JsonSerializer.Serialize(new
            {
                averageConfidence = Math.Round(avgConfidence, 4),
                averageDegreeCentrality = Math.Round(avgCentrality, 4),
                averageBetweennessCentrality = Math.Round(avgBetweenness, 4),
                averageClusteringCoefficient = Math.Round(avgClustering, 4),
                averageDialecticalTemperature = Math.Round(avgDialecticalTemp, 4),
                averageControversyScore = Math.Round(avgControversy, 4),
                averageSchemaEntropy = Math.Round(avgSchemaEntropy, 4),
                graphDensity = Math.Round(density, 6),
                totalEvidenceCount,
                settledCount,
                contestedCount,
                unknownCount,
                unevaluatedCount,
                previousSnapshotId = previous?.Id,
                nodeDelta = previous != null ? nodeCount - previous.NodeCount : 0,
                edgeDelta = previous != null ? edgeCount - previous.EdgeCount : 0,
                schemaDelta = previous != null ? schemaCount - previous.SchemaCount : 0
            }),
            SchemaIdsJson = JsonSerializer.Serialize(schemaIds),
            SynthesisIdsJson = JsonSerializer.Serialize(synthesisIds),
            AverageDialecticalTemperature = Math.Round(avgDialecticalTemp, 4),
            GraphDensity = Math.Round(density, 6)
        };

        db.GraphSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        _logger.LogInformation("Snapshot captured: ID={Id}, {Label}", snapshot.Id, snapshot.Label);
        return snapshot;
    }

    // ── Full Capture Pipeline ─────────────────────────────────────────────

    /// <summary>
    /// Runs the full capture pipeline: recompute metrics, discover schemas,
    /// detect syntheses, then capture snapshot.
    /// </summary>
    public async Task<GraphSnapshot> RunFullCaptureAsync(string? label = null)
    {
        _logger.LogInformation("Starting full graph capture pipeline.");

        // Step 1: Recompute topology metrics
        await _graphService.RecomputeTopologyMetricsAsync();

        // Step 2: Discover schemas via k-means
        await _schemaService.DiscoverSchemasKMeansAsync();

        // Step 3: Capture snapshot
        var snapshot = await CaptureSnapshotAsync(label);

        _logger.LogInformation("Full capture pipeline complete.");
        return snapshot;
    }

    // ── Query ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets all snapshots, most recent first.
    /// </summary>
    public async Task<List<GraphSnapshot>> GetSnapshotsAsync(int count = 20)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.GraphSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the evolution of a specific metric across snapshots.
    /// </summary>
    public async Task<List<MetricEvolution>> GetMetricEvolutionAsync(string metricName)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var snapshots = await db.GraphSnapshots
            .OrderBy(s => s.CapturedAt)
            .ToListAsync();

        var evolution = new List<MetricEvolution>();

        foreach (var snap in snapshots)
        {
            try
            {
                var summary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(snap.TopologySummaryJson);
                if (summary != null && summary.TryGetValue(metricName, out var value))
                {
                    evolution.Add(new MetricEvolution
                    {
                        SnapshotId = snap.Id,
                        CapturedAt = snap.CapturedAt,
                        Label = snap.Label,
                        Value = value.GetDouble()
                    });
                }
            }
            catch { /* skip malformed */ }
        }

        return evolution;
    }

    /// <summary>
    /// Gets the schema evolution — how schema membership changed between
    /// the last two snapshots.
    /// </summary>
    public async Task<SchemaEvolutionResult?> GetSchemaEvolutionAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var snapshots = await db.GraphSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .Take(2)
            .ToListAsync();

        if (snapshots.Count < 2) return null;

        var current = snapshots[0];
        var previous = snapshots[1];

        return new SchemaEvolutionResult
        {
            CurrentSnapshotId = current.Id,
            PreviousSnapshotId = previous.Id,
            CurrentCapturedAt = current.CapturedAt,
            PreviousCapturedAt = previous.CapturedAt,
            NodeGrowth = current.NodeCount - previous.NodeCount,
            EdgeGrowth = current.EdgeCount - previous.EdgeCount,
            SchemaGrowth = current.SchemaCount - previous.SchemaCount,
            TemperatureChange = current.AverageDialecticalTemperature - previous.AverageDialecticalTemperature,
            DensityChange = current.GraphDensity - previous.GraphDensity
        };
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

/// <summary>Evolution of a single metric across snapshots.</summary>
public class MetricEvolution
{
    public int SnapshotId { get; set; }
    public DateTime CapturedAt { get; set; }
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

/// <summary>Schema evolution between two snapshots.</summary>
public class SchemaEvolutionResult
{
    public int CurrentSnapshotId { get; set; }
    public int PreviousSnapshotId { get; set; }
    public DateTime CurrentCapturedAt { get; set; }
    public DateTime PreviousCapturedAt { get; set; }
    public int NodeGrowth { get; set; }
    public int EdgeGrowth { get; set; }
    public int SchemaGrowth { get; set; }
    public double TemperatureChange { get; set; }
    public double DensityChange { get; set; }
}