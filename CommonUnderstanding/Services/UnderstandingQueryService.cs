using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// High-level query API for the Understanding Graph. Provides structured
/// access to schemas, dialectical relationships, bridge nodes, blindspots,
/// and the full graph map for visualization.
/// </summary>
public class UnderstandingQueryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<UnderstandingQueryService> _logger;

    public UnderstandingQueryService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<UnderstandingQueryService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ── Schema Queries ────────────────────────────────────────────────────

    /// <summary>
    /// Gets all schemas with member counts and coherence scores.
    /// </summary>
    public async Task<List<SchemaSummary>> GetAllSchemasAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.ConceptualSchemas
            .Include(s => s.Memberships)
            .Select(s => new SchemaSummary
            {
                Id = s.Id,
                Label = s.Label,
                Description = s.Description,
                DiscoveryMethod = s.DiscoveryMethod,
                Coherence = s.Coherence,
                Stability = s.Stability,
                MemberCount = s.Memberships.Count,
                FactorIndex = s.FactorIndex,
                DiscoveredAt = s.DiscoveredAt
            })
            .OrderByDescending(s => s.MemberCount)
            .ToListAsync();
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

        // Nodes belonging to multiple schemas are bridge nodes
        var bridgeCandidates = await db.SchemaMemberships
            .GroupBy(m => m.NodeId)
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                NodeId = g.Key,
                SchemaCount = g.Count(),
                AvgWeight = g.Average(m => m.Weight)
            })
            .OrderByDescending(x => x.SchemaCount)
            .ThenByDescending(x => x.AvgWeight)
            .Take(count)
            .ToListAsync();

        var result = new List<BridgeNode>();
        foreach (var bc in bridgeCandidates)
        {
            var node = await db.UnderstandingNodes.FindAsync(bc.NodeId);
            if (node == null) continue;

            var schemaIds = await db.SchemaMemberships
                .Where(m => m.NodeId == bc.NodeId)
                .Select(m => m.SchemaId)
                .ToListAsync();

            var schemaLabels = await db.ConceptualSchemas
                .Where(s => schemaIds.Contains(s.Id))
                .Select(s => s.Label)
                .ToListAsync();

            result.Add(new BridgeNode
            {
                NodeId = bc.NodeId,
                CanonicalText = node.CanonicalText,
                SchemaCount = bc.SchemaCount,
                SchemaLabels = schemaLabels,
                BetweennessCentrality = node.BetweennessCentrality,
                DegreeCentrality = node.DegreeCentrality,
                DialecticalTemperature = node.DialecticalTemperature
            });
        }

        return result;
    }

    // ── Blindspots ────────────────────────────────────────────────────────

    /// <summary>
    /// Identifies conceptual blindspots — propositions with high dialectical
    /// temperature but low schema membership (unresolved, isolated ideas).
    /// </summary>
    public async Task<List<Blindspot>> GetBlindspotsAsync(int count = 20)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var nodes = await db.UnderstandingNodes
            .Where(n => n.DialecticalTemperature > 0.3)
            .OrderByDescending(n => n.DialecticalTemperature)
            .Take(count * 3)
            .ToListAsync();

        var result = new List<Blindspot>();
        foreach (var node in nodes)
        {
            var membershipCount = await db.SchemaMemberships
                .CountAsync(m => m.NodeId == node.Id);

            if (membershipCount == 0)
            {
                result.Add(new Blindspot
                {
                    NodeId = node.Id,
                    CanonicalText = node.CanonicalText,
                    DialecticalTemperature = node.DialecticalTemperature,
                    ControversyScore = node.ControversyScore,
                    Confidence = node.Confidence,
                    SchemaEntropy = node.SchemaEntropy,
                    Reason = "Isolated proposition with high dialectical tension"
                });
            }

            if (result.Count >= count) break;
        }

        return result;
    }

    // ── Graph Map ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the full graph map for visualization — nodes, edges, schemas,
    /// and syntheses in a flat serializable structure.
    /// </summary>
    public async Task<GraphMap> GetMapAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var nodes = await db.UnderstandingNodes.ToListAsync();
        var edges = await db.UnderstandingEdges.ToListAsync();
        var schemas = await db.ConceptualSchemas
            .Include(s => s.Memberships)
            .ToListAsync();
        var syntheses = await db.DialecticalSyntheses
            .Include(ds => ds.SynthesisNode)
            .ToListAsync();

        return new GraphMap
        {
            Nodes = nodes.Select(n => new GraphMapNode
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
                CreatedAt = n.FirstSeenAt
            }).ToList(),

            Edges = edges.Select(e => new GraphMapEdge
            {
                Id = e.Id,
                SourceId = e.SourceNodeId,
                TargetId = e.TargetNodeId,
                EdgeType = e.Relationship,
                Weight = e.Weight
            }).ToList(),

            Schemas = schemas.Select(s => new GraphMapSchema
            {
                Id = s.Id,
                Label = s.Label,
                DiscoveryMethod = s.DiscoveryMethod,
                Coherence = s.Coherence,
                Stability = s.Stability,
                MemberNodeIds = s.Memberships.Select(m => m.NodeId).ToList()
            }).ToList(),

            Syntheses = syntheses.Select(ds => new GraphMapSynthesis
            {
                Id = ds.Id,
                SynthesisNodeId = ds.SynthesisNodeId,
                Depth = ds.Depth,
                ResolutionNarrative = ds.ResolutionNarrative,
                IsAccepted = ds.IsAccepted
            }).ToList(),

            Statistics = new GraphMapStatistics
            {
                NodeCount = nodes.Count,
                EdgeCount = edges.Count,
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