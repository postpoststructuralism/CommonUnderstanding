using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Detects contradictions in the Understanding Graph and generates dialectical
/// syntheses — higher-level propositions that resolve opposing viewpoints.
///
/// This is the engine of understanding evolution: the graph grows not just by
/// adding new arguments, but by resolving contradictions at progressively
/// higher levels of abstraction (Hegelian dialectic: thesis → antithesis → synthesis).
///
/// Workflow:
/// 1. Scan for contradiction edges between high-confidence nodes
/// 2. Check if a synthesis already exists for each contradiction pair
/// 3. Generate a synthesis proposition via LLM
/// 4. Add the synthesis as a new UnderstandingNode with synthesizes edges
/// 5. Track the dialectical hierarchy depth
/// </summary>
public class DialecticalSynthesisService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<DialecticalSynthesisService> _logger;

    // Minimum confidence for parent propositions to be considered for synthesis
    private const double MinParentConfidence = 0.7;

    // Maximum depth to prevent infinite recursion
    private const int MaxDepth = 5;

    public DialecticalSynthesisService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<DialecticalSynthesisService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Synthesis detection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans all contradiction edges and generates syntheses for high-confidence
    /// contradictory pairs that don't already have a synthesis.
    /// </summary>
    public async Task<int> GenerateSynthesesAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Find contradiction edges where both nodes have high confidence
        var contradictionEdges = await db.UnderstandingEdges
            .Where(e => e.Relationship == "contradicts" && e.Weight >= 0.5)
            .Include(e => e.SourceNode)
            .Include(e => e.TargetNode)
            .ToListAsync();

        _logger.LogInformation("Found {Count} contradiction edges to evaluate.", contradictionEdges.Count);

        int synthesesCreated = 0;

        foreach (var edge in contradictionEdges)
        {
            var source = edge.SourceNode;
            var target = edge.TargetNode;

            if (source == null || target == null) continue;

            // Both must have sufficient confidence
            if (source.Confidence < MinParentConfidence || target.Confidence < MinParentConfidence)
                continue;

            // Check if a synthesis already exists for this pair
            if (await SynthesisExistsAsync(db, source.Id, target.Id))
                continue;

            // Determine the current depth (max of parents' synthesis depth + 1)
            int depth = await ComputeSynthesisDepthAsync(db, source.Id, target.Id);
            if (depth > MaxDepth)
            {
                _logger.LogDebug("Synthesis depth {Depth} exceeds maximum {Max}; skipping.", depth, MaxDepth);
                continue;
            }

            // Generate synthesis text (placeholder — will use LLM when integrated)
            var synthesisText = GenerateSynthesisText(source.CanonicalText, target.CanonicalText);

            // Create the synthesis UnderstandingNode
            float[]? embedding = null;
            try
            {
                var embeddingService = await GetEmbeddingServiceAsync();
                if (embeddingService != null)
                    embedding = await embeddingService.GenerateEmbeddingAsync(synthesisText);
            }
            catch { _logger.LogDebug("Embedding generation skipped for synthesis."); }

            var synthesisNode = new UnderstandingNode
            {
                CanonicalText = synthesisText,
                NormalizedKey = NormalizeKey(synthesisText),
                Status = PropositionStatus.Unevaluated,
                Confidence = 0.5,
                EvidenceCount = 1,
                SemanticEmbedding = embedding,
                ArgumentIdsJson = "[]",
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.UnderstandingNodes.Add(synthesisNode);
            await db.SaveChangesAsync();

            // Create synthesizes edges from synthesis to both parents
            db.UnderstandingEdges.Add(new UnderstandingEdge
            {
                SourceNodeId = synthesisNode.Id,
                TargetNodeId = source.Id,
                Relationship = "synthesizes",
                Weight = 0.8,
                BaseWeight = 0.8,
                ProvenanceJson = JsonSerializer.Serialize(new
                {
                    method = "dialectical_synthesis",
                    depth = depth,
                    resolvedEdgeId = edge.Id
                }),
                CreatedAt = DateTime.UtcNow,
                LastReinforcedAt = DateTime.UtcNow
            });

            db.UnderstandingEdges.Add(new UnderstandingEdge
            {
                SourceNodeId = synthesisNode.Id,
                TargetNodeId = target.Id,
                Relationship = "synthesizes",
                Weight = 0.8,
                BaseWeight = 0.8,
                ProvenanceJson = JsonSerializer.Serialize(new
                {
                    method = "dialectical_synthesis",
                    depth = depth,
                    resolvedEdgeId = edge.Id
                }),
                CreatedAt = DateTime.UtcNow,
                LastReinforcedAt = DateTime.UtcNow
            });

            // Create DialecticalSynthesis record
            db.DialecticalSyntheses.Add(new DialecticalSynthesis
            {
                SynthesisNodeId = synthesisNode.Id,
                ParentNodeIdsJson = JsonSerializer.Serialize(new List<int> { source.Id, target.Id }),
                ResolvedContradictionIdsJson = JsonSerializer.Serialize(new List<int> { edge.Id }),
                Depth = depth,
                ResolutionNarrative = $"Synthesis of:\n  Thesis: \"{source.CanonicalText}\"\n  Antithesis: \"{target.CanonicalText}\"\n  Synthesis: \"{synthesisText}\"",
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            });

            synthesesCreated++;
            _logger.LogInformation("Created synthesis at depth {Depth}: {Text}", depth, synthesisText[..Math.Min(80, synthesisText.Length)]);
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Synthesis generation complete: {Count} syntheses created.", synthesesCreated);
        return synthesesCreated;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Query
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the full dialectical hierarchy for a given node — all ancestor
    /// syntheses and descendant contradictions.
    /// </summary>
    public async Task<List<DialecticalSynthesis>> GetDialecticalLineageAsync(int nodeId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Find all syntheses where this node is a parent
        var parentSyntheses = await db.DialecticalSyntheses
            .Where(ds => ds.ParentNodeIdsJson.Contains($"\"{nodeId}\""))
            .Include(ds => ds.SynthesisNode)
            .ToListAsync();

        // Find all syntheses where this node is the synthesis
        var childSyntheses = await db.DialecticalSyntheses
            .Where(ds => ds.SynthesisNodeId == nodeId)
            .ToListAsync();

        return parentSyntheses.Concat(childSyntheses)
            .OrderBy(ds => ds.Depth)
            .ThenBy(ds => ds.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Gets the dialectical tree — all syntheses at or below a given depth.
    /// </summary>
    public async Task<List<DialecticalSynthesis>> GetSynthesisTreeAsync(int maxDepth = 3)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.DialecticalSyntheses
            .Where(ds => ds.Depth <= maxDepth)
            .Include(ds => ds.SynthesisNode)
            .OrderBy(ds => ds.Depth)
            .ThenBy(ds => ds.CreatedAt)
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<bool> SynthesisExistsAsync(ApplicationDbContext db, int nodeAId, int nodeBId)
    {
        // A synthesis exists if there's a node with "synthesizes" edges to both parents
        var synthesizersA = await db.UnderstandingEdges
            .Where(e => e.Relationship == "synthesizes" && e.TargetNodeId == nodeAId)
            .Select(e => e.SourceNodeId)
            .ToListAsync();

        var synthesizersB = await db.UnderstandingEdges
            .Where(e => e.Relationship == "synthesizes" && e.TargetNodeId == nodeBId)
            .Select(e => e.SourceNodeId)
            .ToListAsync();

        return synthesizersA.Intersect(synthesizersB).Any();
    }

    private async Task<int> ComputeSynthesisDepthAsync(ApplicationDbContext db, int nodeAId, int nodeBId)
    {
        // Find the maximum depth of any existing synthesis involving these nodes
        var depths = await db.DialecticalSyntheses
            .Where(ds => ds.ParentNodeIdsJson.Contains($"\"{nodeAId}\"") ||
                         ds.ParentNodeIdsJson.Contains($"\"{nodeBId}\""))
            .Select(ds => ds.Depth)
            .ToListAsync();

        int maxParentDepth = depths.Any() ? depths.Max() : -1;
        return maxParentDepth + 1;
    }

    private static string GenerateSynthesisText(string thesis, string antithesis)
    {
        // Placeholder synthesis generation.
        // In production, this will call an LLM via SemanticKernelService.
        // The placeholder uses a simple template.
        return $"A higher-level perspective that reconciles \"{Truncate(thesis, 60)}\" with \"{Truncate(antithesis, 60)}\" by finding common ground at a more abstract level of analysis.";
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private static string NormalizeKey(string text)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
        normalized = normalized.TrimEnd('.', ',', ';', ':', '!', '?');
        return normalized.Length > 500 ? normalized[..500] : normalized;
    }

    private async Task<Services.Social.EmbeddingService?> GetEmbeddingServiceAsync()
    {
        // Resolve EmbeddingService from the service provider
        // This is a workaround since we use IDbContextFactory pattern
        try
        {
            // The EmbeddingService is registered as Scoped; we need to resolve it
            // via the service provider. For now, return null to skip embedding.
            return null;
        }
        catch
        {
            return null;
        }
    }
}