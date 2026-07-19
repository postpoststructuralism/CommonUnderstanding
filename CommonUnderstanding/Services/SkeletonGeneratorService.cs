using System.Text.Json;
using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Generates a versioned static JSON skeleton of the graph's root nodes + edges.
/// This file is served as a static asset (not through a controller), so it costs
/// zero DB queries per page load. Regenerated nightly and after each graph rebuild.
/// </summary>
public class SkeletonGeneratorService
{
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly ILogger<SkeletonGeneratorService> _logger;
    private readonly string _dataDir;

    public SkeletonGeneratorService(
        SingletonDbContextFactory dbFactory,
        ILogger<SkeletonGeneratorService> logger,
        IWebHostEnvironment env)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _dataDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "data");
    }

    /// <summary>
    /// Generates the skeleton JSON file and updates the manifest.
    /// Idempotent — safe to call concurrently with normal request traffic.
    /// </summary>
    public async Task GenerateAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_dataDir);

        var version = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var fileName = $"skeleton-v{version}.json";
        var filePath = Path.Combine(_dataDir, fileName);

        _logger.LogInformation("Generating skeleton {FileName}...", fileName);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Same query as GetRootNodesAsync but without the memory cache layer
        var nodes = await db.UnderstandingNodes
            .Where(n => n.Confidence >= 0.4 && n.DegreeCentrality >= 0.005)
            .OrderByDescending(n => n.DegreeCentrality)
            .ThenByDescending(n => n.Confidence)
            .Take(150)
            .Select(n => new
            {
                n.Id,
                Label = n.CanonicalText,
                n.Confidence,
                n.DegreeCentrality,
                n.BetweennessCentrality,
                n.ClusteringCoefficient,
                n.DialecticalTemperature,
                n.ControversyScore,
                n.SchemaEntropy,
                Status = n.Status.ToString(),
                n.EvidenceCount,
                CreatedAt = n.FirstSeenAt,
                n.ArgumentIdsJson
            })
            .ToListAsync(ct);

        var loadedNodeIds = nodes.Select(n => n.Id).ToHashSet();

        var edges = await db.UnderstandingEdges
            .Where(e => loadedNodeIds.Contains(e.SourceNodeId) && loadedNodeIds.Contains(e.TargetNodeId))
            .OrderByDescending(e => e.Weight)
            .Take(500)
            .Select(e => new
            {
                e.Id,
                SourceId = e.SourceNodeId,
                TargetId = e.TargetNodeId,
                EdgeType = e.Relationship,
                e.Weight
            })
            .ToListAsync(ct);

        // Bake fixed x/y positions using a simple force-directed layout approximation.
        // This avoids running vis-network physics on the client for the skeleton layer.
        var positions = ComputeFixedPositions(nodes.Count);
        var positionedNodes = nodes.Select((n, i) => new
        {
            n.Id,
            n.Label,
            n.Confidence,
            n.DegreeCentrality,
            n.BetweennessCentrality,
            n.ClusteringCoefficient,
            n.DialecticalTemperature,
            n.ControversyScore,
            n.SchemaEntropy,
            n.Status,
            n.EvidenceCount,
            n.CreatedAt,
            n.ArgumentIdsJson,
            x = positions[i].X,
            y = positions[i].Y,
            @fixed = true
        }).ToList();

        var skeleton = new
        {
            nodes = positionedNodes,
            edges = edges,
            generatedAt = DateTime.UtcNow.ToString("o"),
            version = version
        };

        var json = JsonSerializer.Serialize(skeleton, new JsonSerializerOptions { WriteIndented = false });
        await File.WriteAllTextAsync(filePath, json, ct);

        // Update manifest
        var manifest = new
        {
            url = $"/data/{fileName}",
            version = version,
            generatedAt = DateTime.UtcNow.ToString("o"),
            nodeCount = nodes.Count,
            edgeCount = edges.Count
        };
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(_dataDir, "skeleton-manifest.json"), manifestJson, ct);

        // Clean up old skeleton files (keep only the latest 3)
        CleanOldFiles(_dataDir, "skeleton-v*.json", 3);

        _logger.LogInformation("Skeleton {FileName} generated: {NodeCount} nodes, {EdgeCount} edges.",
            fileName, nodes.Count, edges.Count);
    }

    /// <summary>
    /// Computes fixed 2D positions for nodes using a simple circular-layout-with-jitter
    /// approximation. This is a server-side stand-in for vis-network's forceAtlas2Based
    /// solver — good enough for a static skeleton that won't be re-laid-out.
    /// </summary>
    private static List<(double X, double Y)> ComputeFixedPositions(int count)
    {
        var positions = new List<(double X, double Y)>(count);
        var rng = new Random(42); // deterministic seed for reproducibility
        var radius = 300.0;

        for (int i = 0; i < count; i++)
        {
            var angle = 2.0 * Math.PI * i / count;
            var r = radius * (0.6 + 0.4 * rng.NextDouble()); // jitter radius
            positions.Add((Math.Cos(angle) * r, Math.Sin(angle) * r));
        }

        return positions;
    }

    private static void CleanOldFiles(string dir, string pattern, int keepCount)
    {
        try
        {
            var files = Directory.GetFiles(dir, pattern)
                .OrderByDescending(f => f)
                .ToList();

            foreach (var file in files.Skip(keepCount))
            {
                File.Delete(file);
            }
        }
        catch (Exception) { /* best-effort cleanup */ }
    }
}