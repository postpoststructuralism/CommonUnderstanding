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
    /// Gets all snapshots, most recent first. Uses projection to avoid loading
    /// large JSON blob columns (TopologySummaryJson, SchemaIdsJson, SynthesisIdsJson).
    /// </summary>
    public async Task<List<GraphSnapshot>> GetSnapshotsAsync(int count = 20)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.GraphSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .Take(count)
            .Select(s => new GraphSnapshot
            {
                Id = s.Id,
                Label = s.Label,
                NodeCount = s.NodeCount,
                EdgeCount = s.EdgeCount,
                SchemaCount = s.SchemaCount,
                CapturedAt = s.CapturedAt,
                AverageDialecticalTemperature = s.AverageDialecticalTemperature,
                GraphDensity = s.GraphDensity
            })
            .ToListAsync();
    }

    /// <summary>
    /// Builds the reader-facing version history from consecutive snapshots.
    /// </summary>
    public async Task<GraphEvolutionViewModel> GetEvolutionHistoryAsync(int count = 50, int? nodeId = null, int? schemaId = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var snapshots = await db.GraphSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .Take(count)
            .Select(s => new GraphSnapshot
            {
                Id = s.Id,
                Label = s.Label,
                NodeCount = s.NodeCount,
                EdgeCount = s.EdgeCount,
                SchemaCount = s.SchemaCount,
                CapturedAt = s.CapturedAt,
                AverageDialecticalTemperature = s.AverageDialecticalTemperature,
                GraphDensity = s.GraphDensity
            })
            .ToListAsync();

        var chronological = snapshots.OrderBy(s => s.CapturedAt).ToList();
        if (chronological.Count == 0)
        {
            return new GraphEvolutionViewModel();
        }

        var historyStart = chronological[0].CapturedAt.AddDays(-7);
        var historyEnd = chronological[^1].CapturedAt;
        var nodeQuery = db.UnderstandingNodes.AsNoTracking().AsQueryable();
        if (nodeId.HasValue)
        {
            nodeQuery = nodeQuery.Where(n => n.Id == nodeId.Value);
        }
        else if (schemaId.HasValue)
        {
            nodeQuery = nodeQuery.Where(n => n.SchemaMemberships.Any(m => m.SchemaId == schemaId.Value));
        }

        var contentNodes = await nodeQuery
            .Where(n => n.FirstSeenAt <= historyEnd && n.LastUpdatedAt >= historyStart)
            .Select(n => new ZeitgeistClaim
            {
                Id = n.Id,
                Text = n.CanonicalText,
                FirstSeenAt = n.FirstSeenAt,
                LastUpdatedAt = n.LastUpdatedAt,
                Attention = n.DegreeCentrality + n.ControversyScore + n.DialecticalTemperature
            })
            .ToListAsync();

        var nodeIds = contentNodes.Select(n => n.Id).ToList();
        var memberships = await db.SchemaMemberships
            .AsNoTracking()
            .Where(m => nodeIds.Contains(m.NodeId))
            .Select(m => new ZeitgeistMembership
            {
                NodeId = m.NodeId,
                Label = m.Schema.Label,
                Weight = m.Weight
            })
            .ToListAsync();

        var contributions = nodeId.HasValue || schemaId.HasValue
            ? new List<ZeitgeistContribution>()
            : await db.SocialArguments
                .AsNoTracking()
                .Where(a => a.IsPublic && !a.IsShadowBanned && a.UpdatedAt >= historyStart && a.CreatedAt <= historyEnd)
                .Select(a => new ZeitgeistContribution
                {
                    Title = a.Title,
                    Claim = a.ClaimProposition != null ? a.ClaimProposition.Text : a.Title,
                    Tags = a.Tags,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    Attention = a.ReplyCount + a.UpvoteCount + a.DownvoteCount
                })
                .ToListAsync();

        var moments = new List<GraphEvolutionMoment>();
        for (var index = 0; index < chronological.Count; index++)
        {
            var current = chronological[index];
            var previous = index > 0 ? chronological[index - 1] : null;
            var intervalStart = previous?.CapturedAt ?? current.CapturedAt.AddDays(-7);
            moments.Add(new GraphEvolutionMoment
            {
                Snapshot = current,
                NodeDelta = previous is null ? 0 : current.NodeCount - previous.NodeCount,
                EdgeDelta = previous is null ? 0 : current.EdgeCount - previous.EdgeCount,
                SchemaDelta = previous is null ? 0 : current.SchemaCount - previous.SchemaCount,
                TemperatureDelta = previous is null ? 0 : current.AverageDialecticalTemperature - previous.AverageDialecticalTemperature,
                Content = BuildZeitgeistSummary(intervalStart, current.CapturedAt, contentNodes, memberships, contributions)
            });
        }

        var weekStart = DateTime.UtcNow.AddDays(-7);
        var weeklyMoments = moments.Where(m => m.Snapshot.CapturedAt >= weekStart).ToList();
        return new GraphEvolutionViewModel
        {
            Moments = moments.OrderByDescending(m => m.Snapshot.CapturedAt).ToList(),
            Digest = new GraphEvolutionDigest
            {
                StartDate = weekStart,
                EndDate = DateTime.UtcNow,
                SnapshotCount = weeklyMoments.Count,
                NodeDelta = weeklyMoments.Sum(m => m.NodeDelta),
                EdgeDelta = weeklyMoments.Sum(m => m.EdgeDelta),
                SchemaDelta = weeklyMoments.Sum(m => m.SchemaDelta),
                TemperatureDelta = weeklyMoments.Sum(m => m.TemperatureDelta),
                Content = BuildZeitgeistSummary(weekStart, DateTime.UtcNow, contentNodes, memberships, contributions)
            }
        };
    }

    private static ZeitgeistSummary BuildZeitgeistSummary(
        DateTime start,
        DateTime end,
        IReadOnlyCollection<ZeitgeistClaim> nodes,
        IReadOnlyCollection<ZeitgeistMembership> memberships,
        IReadOnlyCollection<ZeitgeistContribution> contributions)
    {
        var activeNodes = nodes
            .Where(n => (n.FirstSeenAt > start && n.FirstSeenAt <= end) || (n.LastUpdatedAt > start && n.LastUpdatedAt <= end))
            .OrderByDescending(n => n.Attention)
            .ToList();
        var activeNodeIds = activeNodes.Select(n => n.Id).ToHashSet();
        var activeContributions = contributions
            .Where(c => (c.CreatedAt > start && c.CreatedAt <= end) || (c.UpdatedAt > start && c.UpdatedAt <= end))
            .OrderByDescending(c => c.Attention)
            .ToList();

        var themes = activeContributions
            .SelectMany(c => c.Tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Concat(memberships
                .Where(m => activeNodeIds.Contains(m.NodeId) && !string.IsNullOrWhiteSpace(m.Label))
                .OrderByDescending(m => m.Weight)
                .Select(m => m.Label.Trim()))
            .Where(IsMeaningfulTheme)
            .GroupBy(topic => topic, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(3)
            .Select(group => new ZeitgeistTheme { Name = group.Key, ActivityCount = group.Count() })
            .ToList();

        var notableClaims = activeContributions
            .Select(c => string.IsNullOrWhiteSpace(c.Claim) ? c.Title : c.Claim)
            .Concat(activeNodes.Select(n => n.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return new ZeitgeistSummary
        {
            Themes = themes,
            NotableClaims = notableClaims,
            ContributionCount = activeContributions.Count,
            NewClaimCount = activeNodes.Count(n => n.FirstSeenAt > start && n.FirstSeenAt <= end),
            UpdatedClaimCount = activeNodes.Count(n => n.FirstSeenAt <= start && n.LastUpdatedAt > start && n.LastUpdatedAt <= end)
        };
    }

    private static bool IsMeaningfulTheme(string theme)
    {
        if (!theme.StartsWith("Schema ", StringComparison.OrdinalIgnoreCase)) return true;

        var suffix = theme["Schema ".Length..];
        var identifier = suffix.Split(' ', '(', StringSplitOptions.RemoveEmptyEntries)[0];
        return !int.TryParse(identifier, out _);
    }

    /// <summary>
    /// Gets the evolution of a specific metric across snapshots.
    /// Uses projection + Take() to limit data transfer.
    /// </summary>
    public async Task<List<MetricEvolution>> GetMetricEvolutionAsync(string metricName)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var snapshots = await db.GraphSnapshots
            .OrderBy(s => s.CapturedAt)
            .Take(200) // Reasonable limit for a chart
            .Select(s => new { s.Id, s.CapturedAt, s.Label, s.TopologySummaryJson })
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
    /// the last two snapshots. Uses projection to avoid loading JSON blobs.
    /// </summary>
    public async Task<SchemaEvolutionResult?> GetSchemaEvolutionAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var snapshots = await db.GraphSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .Take(2)
            .Select(s => new
            {
                s.Id,
                s.CapturedAt,
                s.NodeCount,
                s.EdgeCount,
                s.SchemaCount,
                s.AverageDialecticalTemperature,
                s.GraphDensity
            })
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

public class GraphEvolutionViewModel
{
    public List<GraphEvolutionMoment> Moments { get; set; } = new();
    public GraphEvolutionDigest Digest { get; set; } = new();
}

public class GraphEvolutionMoment
{
    public GraphSnapshot Snapshot { get; set; } = null!;
    public int NodeDelta { get; set; }
    public int EdgeDelta { get; set; }
    public int SchemaDelta { get; set; }
    public double TemperatureDelta { get; set; }
    public ZeitgeistSummary Content { get; set; } = new();
}

public class GraphEvolutionDigest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SnapshotCount { get; set; }
    public int NodeDelta { get; set; }
    public int EdgeDelta { get; set; }
    public int SchemaDelta { get; set; }
    public double TemperatureDelta { get; set; }
    public ZeitgeistSummary Content { get; set; } = new();
}

public class ZeitgeistSummary
{
    public List<ZeitgeistTheme> Themes { get; set; } = new();
    public List<string> NotableClaims { get; set; } = new();
    public int ContributionCount { get; set; }
    public int NewClaimCount { get; set; }
    public int UpdatedClaimCount { get; set; }
    public bool HasActivity => ContributionCount > 0 || NewClaimCount > 0 || UpdatedClaimCount > 0;
}

public class ZeitgeistTheme
{
    public string Name { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
}

internal class ZeitgeistClaim
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public double Attention { get; set; }
}

internal class ZeitgeistMembership
{
    public int NodeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public double Weight { get; set; }
}

internal class ZeitgeistContribution
{
    public string Title { get; set; } = string.Empty;
    public string Claim { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Attention { get; set; }
}