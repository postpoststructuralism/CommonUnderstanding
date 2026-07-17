using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CommonUnderstanding.Services;

/// <summary>
/// High-level query API for the Understanding Graph. Provides structured
/// access to schemas, dialectical relationships, bridge nodes, blindspots,
/// and the full graph map for visualization.
/// </summary>
public class UnderstandingQueryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UnderstandingQueryService> _logger;

    private static readonly TimeSpan MapCacheDuration = TimeSpan.FromMinutes(5);
    private const string MapCacheKey = "graph:map:full";
    private const string RootNodesCacheKey = "graph:map:roots";

    public UnderstandingQueryService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IMemoryCache cache,
        ILogger<UnderstandingQueryService> logger)
    {
        _contextFactory = contextFactory;
        _cache = cache;
        _logger = logger;
    }

    // ── Schema Queries ────────────────────────────────────────────────────

    /// <summary>
    /// Gets quick counts for the initial page load — avoids loading any
    /// entity data, just runs COUNT queries.
    /// </summary>
    public async Task<QuickStats> GetQuickStatsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Run counts sequentially — DbContext is not thread-safe
        var nodeCount = await db.UnderstandingNodes.CountAsync();
        var edgeCount = await db.UnderstandingEdges.CountAsync();
        var schemaCount = await db.ConceptualSchemas.CountAsync();
        var synthesisCount = await db.DialecticalSyntheses.CountAsync();

        return new QuickStats
        {
            NodeCount = nodeCount,
            EdgeCount = edgeCount,
            SchemaCount = schemaCount,
            SynthesisCount = synthesisCount
        };
    }

    /// <summary>
    /// Gets all schemas with member counts and coherence scores.
    /// Uses a grouped count query instead of eager-loading all memberships.
    /// </summary>
    public async Task<List<SchemaSummary>> GetAllSchemasAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Get member counts via a single grouped query instead of Include()
        var memberCounts = await db.SchemaMemberships
            .GroupBy(m => m.SchemaId)
            .Select(g => new { SchemaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SchemaId, x => x.Count);

        var schemas = await db.ConceptualSchemas
            .OrderByDescending(s => s.Coherence)
            .Select(s => new SchemaSummary
            {
                Id = s.Id,
                Label = s.Label,
                Description = s.Description,
                DiscoveryMethod = s.DiscoveryMethod,
                Coherence = s.Coherence,
                Stability = s.Stability,
                MemberCount = 0, // populated below
                FactorIndex = s.FactorIndex,
                DiscoveredAt = s.DiscoveredAt
            })
            .ToListAsync();

        foreach (var schema in schemas)
        {
            schema.MemberCount = memberCounts.GetValueOrDefault(schema.Id, 0);
        }

        // Re-sort by member count after populating
        return schemas.OrderByDescending(s => s.MemberCount).ToList();
    }

    /// <summary>
    /// Gets the schema for a specific node — returns the schema(s) the node
    /// belongs to with membership weight.
    /// </summary>
    public async Task<List<NodeSchemaMembership>> GetSchemaForNodeAsync(int nodeId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var memberships = await db.SchemaMemberships
            .Include(m => m.Schema)
            .Where(m => m.NodeId == nodeId)
            .ToListAsync();

        return memberships.Select(m => new NodeSchemaMembership
        {
            SchemaId = m.SchemaId,
            SchemaLabel = m.Schema?.Label ?? "Unknown",
            Weight = m.Weight
        }).ToList();
    }

    /// <summary>
    /// Gets all nodes belonging to a schema, ordered by membership weight.
    /// </summary>
    public async Task<List<SchemaNodeDetail>> GetSchemaNodesAsync(int schemaId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var memberships = await db.SchemaMemberships
            .Include(m => m.Node)
            .Where(m => m.SchemaId == schemaId)
            .OrderByDescending(m => m.Weight)
            .ToListAsync();

        return memberships.Select(m => new SchemaNodeDetail
        {
            NodeId = m.NodeId,
            CanonicalText = m.Node?.CanonicalText ?? "Unknown",
            Confidence = m.Node?.Confidence ?? 0,
            Weight = m.Weight,
            DegreeCentrality = m.Node?.DegreeCentrality ?? 0,
            DialecticalTemperature = m.Node?.DialecticalTemperature ?? 0
        }).ToList();
    }

    // ── Dialectical Queries ───────────────────────────────────────────────

    /// <summary>
    /// Gets dialectical pairs — edges with high weight or contradiction
    /// relationships, ordered by weight descending.
    /// </summary>
    public async Task<List<DialecticalPair>> GetDialecticalPairsAsync(
        double minWeight = 0.3, int count = 50)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var edges = await db.UnderstandingEdges
            .Include(e => e.SourceNode)
            .Include(e => e.TargetNode)
            .Where(e => e.Weight >= minWeight)
            .OrderByDescending(e => e.Weight)
            .Take(count)
            .ToListAsync();

        return edges.Select(e => new DialecticalPair
        {
            SourceId = e.SourceNodeId,
            SourceLabel = e.SourceNode?.CanonicalText ?? "Unknown",
            TargetId = e.TargetNodeId,
            TargetLabel = e.TargetNode?.CanonicalText ?? "Unknown",
            Relationship = e.Relationship,
            Weight = e.Weight
        }).ToList();
    }

    /// <summary>
    /// Gets the synthesis chain for a given synthesis — the full lineage
    /// of parent propositions → synthesis.
    /// </summary>
    public async Task<SynthesisChain?> GetSynthesisChainAsync(int synthesisId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var synthesis = await db.DialecticalSyntheses
            .Include(ds => ds.SynthesisNode)
            .FirstOrDefaultAsync(ds => ds.Id == synthesisId);

        if (synthesis == null) return null;

        // Parse parent node IDs from JSON
        var parentNodeIds = JsonSerializer.Deserialize<List<int>>(synthesis.ParentNodeIdsJson) ?? new();
        var parentNodes = parentNodeIds.Count > 0
            ? await db.UnderstandingNodes.Where(n => parentNodeIds.Contains(n.Id)).ToListAsync()
            : new List<UnderstandingNode>();

        return new SynthesisChain
        {
            SynthesisId = synthesis.Id,
            ParentNodeIds = parentNodeIds,
            ParentLabels = parentNodes.Select(n => n.CanonicalText).ToList(),
            SynthesisNodeId = synthesis.SynthesisNodeId,
            SynthesisLabel = synthesis.SynthesisNode?.CanonicalText ?? "Unknown",
            Depth = synthesis.Depth,
            ResolutionNarrative = synthesis.ResolutionNarrative,
            IsAccepted = synthesis.IsAccepted,
            CreatedAt = synthesis.CreatedAt
        };
    }

    /// <summary>
    /// Gets all syntheses, ordered by depth (deepest first).
    /// </summary>
    public async Task<List<SynthesisSummary>> GetAllSynthesesAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var syntheses = await db.DialecticalSyntheses
            .Include(ds => ds.SynthesisNode)
            .OrderByDescending(ds => ds.Depth)
            .ThenByDescending(ds => ds.CreatedAt)
            .ToListAsync();

        return syntheses.Select(ds =>
        {
            var parentNodeIds = JsonSerializer.Deserialize<List<int>>(ds.ParentNodeIdsJson) ?? new();
            return new SynthesisSummary
            {
                Id = ds.Id,
                ParentNodeIds = parentNodeIds,
                SynthesisLabel = ds.SynthesisNode?.CanonicalText ?? "Unknown",
                Depth = ds.Depth,
                ResolutionNarrative = ds.ResolutionNarrative,
                IsAccepted = ds.IsAccepted,
                CreatedAt = ds.CreatedAt
            };
        }).ToList();
    }

    // ── Bridge Nodes ──────────────────────────────────────────────────────

    /// <summary>
    /// Finds bridge nodes — nodes that connect multiple schemas, acting as
    /// conceptual bridges between different belief systems.
    /// </summary>
    public async Task<List<BridgeNode>> GetBridgeNodesAsync(int count = 20)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Single query: nodes belonging to multiple schemas with their labels
        var bridgeData = await db.SchemaMemberships
            .GroupBy(m => m.NodeId)
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                NodeId = g.Key,
                SchemaCount = g.Count(),
                AvgWeight = g.Average(m => m.Weight),
                SchemaIds = g.Select(m => m.SchemaId).ToList()
            })
            .OrderByDescending(x => x.SchemaCount)
            .ThenByDescending(x => x.AvgWeight)
            .Take(count)
            .Join(
                db.UnderstandingNodes,
                bc => bc.NodeId,
                n => n.Id,
                (bc, n) => new { bc, n }
            )
            .ToListAsync();

        // Batch-load all schema labels
        var allSchemaIds = bridgeData.SelectMany(b => b.bc.SchemaIds).Distinct().ToList();
        var schemaLabels = await db.ConceptualSchemas
            .Where(s => allSchemaIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Label);

        return bridgeData.Select(b => new BridgeNode
        {
            NodeId = b.bc.NodeId,
            CanonicalText = b.n.CanonicalText,
            SchemaCount = b.bc.SchemaCount,
            SchemaLabels = b.bc.SchemaIds.Select(id => schemaLabels.GetValueOrDefault(id, "Unknown")).ToList(),
            BetweennessCentrality = b.n.BetweennessCentrality,
            DegreeCentrality = b.n.DegreeCentrality,
            DialecticalTemperature = b.n.DialecticalTemperature
        }).ToList();
    }

    // ── Blindspots ────────────────────────────────────────────────────────

    /// <summary>
    /// Identifies conceptual blindspots — propositions with high dialectical
    /// temperature but low schema membership (unresolved, isolated ideas).
    /// </summary>
    public async Task<List<Blindspot>> GetBlindspotsAsync(int count = 20)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Single query: nodes with high dialectical temperature and no schema membership
        var nodeIdsWithMembership = db.SchemaMemberships.Select(m => m.NodeId);

        var result = await db.UnderstandingNodes
            .Where(n => n.DialecticalTemperature > 0.3 && !nodeIdsWithMembership.Contains(n.Id))
            .OrderByDescending(n => n.DialecticalTemperature)
            .Take(count)
            .Select(n => new Blindspot
            {
                NodeId = n.Id,
                CanonicalText = n.CanonicalText,
                DialecticalTemperature = n.DialecticalTemperature,
                ControversyScore = n.ControversyScore,
                Confidence = n.Confidence,
                SchemaEntropy = n.SchemaEntropy,
                Reason = "Isolated proposition with high dialectical tension"
            })
            .ToListAsync();

        return result;
    }

    // ── Graph Map ─────────────────────────────────────────────────────────

    /// <summary>
    /// <summary>
    /// Gets the full graph map for visualization — nodes, edges, schemas,
    /// and syntheses in a flat serializable structure.
    /// Uses projections to avoid loading unnecessary columns and
    /// limits results to keep the payload manageable.
    /// </summary>
    public async Task<GraphMap> GetMapAsync(int maxNodes = 2000, int maxEdges = 5000)
    {
        // Try cache first
        if (_cache.TryGetValue(MapCacheKey, out GraphMap? cached) && cached != null)
        {
            _logger.LogDebug("Graph map served from cache: {NodeCount} nodes, {EdgeCount} edges",
                cached.Nodes.Count, cached.Edges.Count);
            return cached;
        }

        var map = await BuildMapAsync(maxNodes, maxEdges);
        _cache.Set(MapCacheKey, map, MapCacheDuration);
        _logger.LogInformation("Graph map built and cached: {NodeCount} nodes, {EdgeCount} edges",
            map.Nodes.Count, map.Edges.Count);
        return map;
    }

    /// <summary>
    /// Invalidates the graph map cache — call after mutations (new nodes, edges, schemas, etc.).
    /// </summary>
    public void InvalidateMapCache()
    {
        _cache.Remove(MapCacheKey);
        _cache.Remove(RootNodesCacheKey);
    }

    /// <summary>
    /// Gets only the "root" nodes — high-confidence, high-centrality nodes that form the
    /// skeleton of the graph. These are the least likely to change and give immediate
    /// visual structure. The frontend loads these first, then streams in leaf nodes.
    /// </summary>
    public async Task<GraphMap> GetRootNodesAsync(int count = 150)
    {
        if (_cache.TryGetValue(RootNodesCacheKey, out GraphMap? cached) && cached != null)
            return cached;

        await using var db = await _contextFactory.CreateDbContextAsync();

        // Root nodes: high confidence + high centrality, ordered by degree centrality desc.
        // These are the "backbone" propositions that everything else connects to.
        var nodes = await db.UnderstandingNodes
            .Where(n => n.Confidence >= 0.4 && n.DegreeCentrality >= 0.005)
            .OrderByDescending(n => n.DegreeCentrality)
            .ThenByDescending(n => n.Confidence)
            .Take(count)
            .Select(n => new GraphMapNode
            {
                Id = n.Id,
                Label = n.CanonicalText,
                Confidence = n.Confidence,
                DegreeCentrality = n.DegreeCentrality,
                BetweennessCentrality = n.BetweennessCentrality,
                ClusteringCoefficient = n.ClusteringCoefficient,
                DialecticalTemperature = n.DialecticalTemperature,
                ControversyScore = n.ControversyScore,
                SchemaEntropy = n.SchemaEntropy,
                Status = n.Status.ToString(),
                EvidenceCount = n.EvidenceCount,
                CreatedAt = n.FirstSeenAt,
                ArgumentIdsJson = n.ArgumentIdsJson
            })
            .ToListAsync();

        var loadedNodeIds = nodes.Select(n => n.Id).ToHashSet();

        // Only edges between root nodes — keeps the skeleton clean
        var edges = await db.UnderstandingEdges
            .Where(e => loadedNodeIds.Contains(e.SourceNodeId) && loadedNodeIds.Contains(e.TargetNodeId))
            .OrderByDescending(e => e.Weight)
            .Take(500)
            .Select(e => new GraphMapEdge
            {
                Id = e.Id,
                SourceId = e.SourceNodeId,
                TargetId = e.TargetNodeId,
                EdgeType = e.Relationship,
                Weight = e.Weight
            })
            .ToListAsync();

        var totalNodeCount = await db.UnderstandingNodes.CountAsync();
        var totalEdgeCount = await db.UnderstandingEdges.CountAsync();

        var map = new GraphMap
        {
            Nodes = nodes,
            Edges = edges,
            Schemas = new(),  // not needed for root view
            Syntheses = new(),
            Statistics = new GraphMapStatistics
            {
                NodeCount = totalNodeCount,
                EdgeCount = totalEdgeCount,
                SchemaCount = 0,
                SynthesisCount = 0,
                AverageConfidence = nodes.Any() ? Math.Round(nodes.Average(n => n.Confidence), 4) : 0,
                AverageDialecticalTemperature = nodes.Any() ? Math.Round(nodes.Average(n => n.DialecticalTemperature), 4) : 0,
                AverageControversy = nodes.Any() ? Math.Round(nodes.Average(n => n.ControversyScore), 4) : 0
            }
        };

        _cache.Set(RootNodesCacheKey, map, MapCacheDuration);
        return map;
    }

    /// <summary>
    /// Gets leaf nodes — nodes NOT in the provided root set. Used for progressive loading.
    /// </summary>
    public async Task<GraphMap> GetLeafNodesAsync(HashSet<int> rootNodeIds, int maxNodes = 2000)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var nodes = await db.UnderstandingNodes
            .Where(n => !rootNodeIds.Contains(n.Id))
            .OrderByDescending(n => n.Confidence)
            .Take(maxNodes)
            .Select(n => new GraphMapNode
            {
                Id = n.Id,
                Label = n.CanonicalText,
                Confidence = n.Confidence,
                DegreeCentrality = n.DegreeCentrality,
                BetweennessCentrality = n.BetweennessCentrality,
                ClusteringCoefficient = n.ClusteringCoefficient,
                DialecticalTemperature = n.DialecticalTemperature,
                ControversyScore = n.ControversyScore,
                SchemaEntropy = n.SchemaEntropy,
                Status = n.Status.ToString(),
                EvidenceCount = n.EvidenceCount,
                CreatedAt = n.FirstSeenAt,
                ArgumentIdsJson = n.ArgumentIdsJson
            })
            .ToListAsync();

        var allNodeIds = rootNodeIds.Concat(nodes.Select(n => n.Id)).ToHashSet();

        // Edges where at least one endpoint is a leaf (the other can be root or leaf)
        var edges = await db.UnderstandingEdges
            .Where(e => allNodeIds.Contains(e.SourceNodeId) && allNodeIds.Contains(e.TargetNodeId))
            .OrderByDescending(e => e.Weight)
            .Take(5000)
            .Select(e => new GraphMapEdge
            {
                Id = e.Id,
                SourceId = e.SourceNodeId,
                TargetId = e.TargetNodeId,
                EdgeType = e.Relationship,
                Weight = e.Weight
            })
            .ToListAsync();

        return new GraphMap
        {
            Nodes = nodes,
            Edges = edges,
            Schemas = new(),
            Syntheses = new(),
            Statistics = new GraphMapStatistics()
        };
    }

    /// <summary>
    /// Builds the full graph map from the database (uncached).
    /// </summary>
    private async Task<GraphMap> BuildMapAsync(int maxNodes = 2000, int maxEdges = 5000)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Use projections to select only needed columns — avoids loading
        // SemanticEmbedding, GraphEmbedding, and other heavy columns.
        var nodeQuery = db.UnderstandingNodes
            .OrderByDescending(n => n.Confidence)
            .Select(n => new GraphMapNode
            {
                Id = n.Id,
                Label = n.CanonicalText,
                Confidence = n.Confidence,
                DegreeCentrality = n.DegreeCentrality,
                BetweennessCentrality = n.BetweennessCentrality,
                ClusteringCoefficient = n.ClusteringCoefficient,
                DialecticalTemperature = n.DialecticalTemperature,
                ControversyScore = n.ControversyScore,
                SchemaEntropy = n.SchemaEntropy,
                Status = n.Status.ToString(),
                EvidenceCount = n.EvidenceCount,
                CreatedAt = n.FirstSeenAt,
                ArgumentIdsJson = n.ArgumentIdsJson
            });

        var nodes = maxNodes > 0
            ? await nodeQuery.Take(maxNodes).ToListAsync()
            : await nodeQuery.ToListAsync();

        // Only load edges that connect the loaded nodes
        var loadedNodeIds = nodes.Select(n => n.Id).ToHashSet();
        var edgeQuery = db.UnderstandingEdges
            .Where(e => loadedNodeIds.Contains(e.SourceNodeId) || loadedNodeIds.Contains(e.TargetNodeId))
            .OrderByDescending(e => e.Weight)
            .Select(e => new GraphMapEdge
            {
                Id = e.Id,
                SourceId = e.SourceNodeId,
                TargetId = e.TargetNodeId,
                EdgeType = e.Relationship,
                Weight = e.Weight
            });

        var edges = maxEdges > 0
            ? await edgeQuery.Take(maxEdges).ToListAsync()
            : await edgeQuery.ToListAsync();

        // Schemas: use projection, avoid Include(Memberships)
        var schemaMemberships = await db.SchemaMemberships
            .Select(m => new { m.SchemaId, m.NodeId })
            .ToListAsync();

        var membershipLookup = schemaMemberships
            .GroupBy(m => m.SchemaId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.NodeId).ToList());

        var schemas = await db.ConceptualSchemas
            .OrderByDescending(s => s.Coherence)
            .Select(s => new GraphMapSchema
            {
                Id = s.Id,
                Label = s.Label,
                DiscoveryMethod = s.DiscoveryMethod,
                Coherence = s.Coherence,
                Stability = s.Stability,
                MemberNodeIds = new List<int>() // populated below
            })
            .ToListAsync();

        foreach (var schema in schemas)
        {
            schema.MemberNodeIds = membershipLookup.GetValueOrDefault(schema.Id, new List<int>());
        }

        // Syntheses: use projection
        var syntheses = await db.DialecticalSyntheses
            .OrderByDescending(ds => ds.Depth)
            .Select(ds => new GraphMapSynthesis
            {
                Id = ds.Id,
                SynthesisNodeId = ds.SynthesisNodeId,
                Depth = ds.Depth,
                ResolutionNarrative = ds.ResolutionNarrative,
                IsAccepted = ds.IsAccepted
            })
            .ToListAsync();

        // Compute stats efficiently
        var totalNodeCount = await db.UnderstandingNodes.CountAsync();
        var totalEdgeCount = await db.UnderstandingEdges.CountAsync();

        return new GraphMap
        {
            Nodes = nodes,
            Edges = edges,
            Schemas = schemas,
            Syntheses = syntheses,
            Statistics = new GraphMapStatistics
            {
                NodeCount = totalNodeCount,
                EdgeCount = totalEdgeCount,
                SchemaCount = schemas.Count,
                SynthesisCount = syntheses.Count,
                AverageConfidence = nodes.Any() ? Math.Round(nodes.Average(n => n.Confidence), 4) : 0,
                AverageDialecticalTemperature = nodes.Any() ? Math.Round(nodes.Average(n => n.DialecticalTemperature), 4) : 0,
                AverageControversy = nodes.Any() ? Math.Round(nodes.Average(n => n.ControversyScore), 4) : 0
            }
        };
    }

    // ── Search ────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches nodes by canonical text.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int count = 20)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var lower = query.ToLowerInvariant();

        var nodes = await db.UnderstandingNodes
            .Where(n => n.CanonicalText.ToLower().Contains(lower))
            .Take(count)
            .ToListAsync();

        return nodes.Select(n => new SearchResult
        {
            NodeId = n.Id,
            CanonicalText = n.CanonicalText,
            Confidence = n.Confidence,
            DegreeCentrality = n.DegreeCentrality,
            Status = n.Status.ToString(),
            SchemaCount = db.SchemaMemberships.Count(m => m.NodeId == n.Id)
        }).ToList();
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

public class QuickStats
{
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int SchemaCount { get; set; }
    public int SynthesisCount { get; set; }
}

public class SchemaSummary
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscoveryMethod { get; set; } = string.Empty;
    public double Coherence { get; set; }
    public double Stability { get; set; }
    public int MemberCount { get; set; }
    public int? FactorIndex { get; set; }
    public DateTime DiscoveredAt { get; set; }
}

public class NodeSchemaMembership
{
    public int SchemaId { get; set; }
    public string SchemaLabel { get; set; } = string.Empty;
    public double Weight { get; set; }
}

public class SchemaNodeDetail
{
    public int NodeId { get; set; }
    public string CanonicalText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double Weight { get; set; }
    public double DegreeCentrality { get; set; }
    public double DialecticalTemperature { get; set; }
}

public class DialecticalPair
{
    public int SourceId { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string TargetLabel { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public double Weight { get; set; }
}

public class SynthesisChain
{
    public int SynthesisId { get; set; }
    public List<int> ParentNodeIds { get; set; } = new();
    public List<string> ParentLabels { get; set; } = new();
    public int SynthesisNodeId { get; set; }
    public string SynthesisLabel { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string ResolutionNarrative { get; set; } = string.Empty;
    public bool IsAccepted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SynthesisSummary
{
    public int Id { get; set; }
    public List<int> ParentNodeIds { get; set; } = new();
    public string SynthesisLabel { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string ResolutionNarrative { get; set; } = string.Empty;
    public bool IsAccepted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BridgeNode
{
    public int NodeId { get; set; }
    public string CanonicalText { get; set; } = string.Empty;
    public int SchemaCount { get; set; }
    public List<string> SchemaLabels { get; set; } = new();
    public double BetweennessCentrality { get; set; }
    public double DegreeCentrality { get; set; }
    public double DialecticalTemperature { get; set; }
}

public class Blindspot
{
    public int NodeId { get; set; }
    public string CanonicalText { get; set; } = string.Empty;
    public double DialecticalTemperature { get; set; }
    public double ControversyScore { get; set; }
    public double Confidence { get; set; }
    public double SchemaEntropy { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SearchResult
{
    public int NodeId { get; set; }
    public string CanonicalText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double DegreeCentrality { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SchemaCount { get; set; }
}

public class GraphMap
{
    public List<GraphMapNode> Nodes { get; set; } = new();
    public List<GraphMapEdge> Edges { get; set; } = new();
    public List<GraphMapSchema> Schemas { get; set; } = new();
    public List<GraphMapSynthesis> Syntheses { get; set; } = new();
    public GraphMapStatistics Statistics { get; set; } = new();
}

public class GraphMapNode
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double DegreeCentrality { get; set; }
    public double BetweennessCentrality { get; set; }
    public double ClusteringCoefficient { get; set; }
    public double DialecticalTemperature { get; set; }
    public double ControversyScore { get; set; }
    public double SchemaEntropy { get; set; }
    public string Status { get; set; } = string.Empty;
    public int EvidenceCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ArgumentIdsJson { get; set; } = "[]";
}

public class GraphMapEdge
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string EdgeType { get; set; } = string.Empty;
    public double Weight { get; set; }
}

public class GraphMapSchema
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string DiscoveryMethod { get; set; } = string.Empty;
    public double Coherence { get; set; }
    public double Stability { get; set; }
    public List<int> MemberNodeIds { get; set; } = new();
}

public class GraphMapSynthesis
{
    public int Id { get; set; }
    public int SynthesisNodeId { get; set; }
    public int Depth { get; set; }
    public string ResolutionNarrative { get; set; } = string.Empty;
    public bool IsAccepted { get; set; }
}

public class GraphMapStatistics
{
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int SchemaCount { get; set; }
    public int SynthesisCount { get; set; }
    public double AverageConfidence { get; set; }
    public double AverageDialecticalTemperature { get; set; }
    public double AverageControversy { get; set; }
}