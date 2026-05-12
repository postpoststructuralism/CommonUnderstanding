using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

/// <summary>
/// Maintains the evolving organizational Common Understanding Graph.
/// 
/// Each time an argument is adjudicated, its propositions are synced into the graph
/// as versioned nodes. Nodes are deduplicated by normalized text key, so the same
/// proposition appearing in multiple arguments shares a single node whose confidence
/// and status are updated over time.
/// </summary>
public class CommonUnderstandingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CommonUnderstandingService> _logger;

    public CommonUnderstandingService(
        ApplicationDbContext db,
        ILogger<CommonUnderstandingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Sync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Synchronises all propositions from an argument into the Common Understanding Graph.
    /// Called after AdjudicationEngine runs. Safe to call multiple times (idempotent).
    /// </summary>
    public async Task SyncFromArgumentAsync(int argumentId)
    {
        var argument = await _db.Arguments
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
                    .ThenInclude(p => p.EvidenceItems)
            .FirstOrDefaultAsync(a => a.Id == argumentId);

        if (argument == null) return;

        _logger.LogInformation("Syncing argument {Id} into Common Understanding Graph", argumentId);

        foreach (var claim in argument.Claims)
        {
            foreach (var proposition in claim.Premises)
            {
                await UpsertNodeAsync(proposition, argumentId);
            }
        }

        _logger.LogInformation("Graph sync complete for argument {Id}", argumentId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Query
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all graph nodes ordered by confidence descending.</summary>
    public async Task<List<CommonUnderstandingNode>> GetAllAsync()
    {
        return await _db.CommonUnderstandingNodes
            .OrderByDescending(n => n.Confidence)
            .ThenBy(n => n.Status)
            .ToListAsync();
    }

    /// <summary>
    /// Searches nodes whose text contains any of the terms in the query.
    /// </summary>
    public async Task<List<CommonUnderstandingNode>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync();

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.ToLowerInvariant())
                         .ToList();

        // EF Core SQLite doesn't support complex LINQ — materialise and filter in memory
        var allNodes = await _db.CommonUnderstandingNodes.ToListAsync();

        return allNodes
            .Where(n => terms.Any(t => n.Text.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(n => n.Confidence)
            .ToList();
    }

    /// <summary>Returns nodes grouped by their current status.</summary>
    public async Task<Dictionary<PropositionStatus, List<CommonUnderstandingNode>>> GetGroupedByStatusAsync()
    {
        var nodes = await GetAllAsync();
        return nodes
            .GroupBy(n => n.Status)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Returns a single node with its connected edges and related nodes.</summary>
    public async Task<CommonUnderstandingNode?> GetWithEdgesAsync(int nodeId)
    {
        return await _db.CommonUnderstandingNodes
            .Include(n => n.OutboundEdges)
                .ThenInclude(e => e.TargetNode)
            .Include(n => n.InboundEdges)
                .ThenInclude(e => e.SourceNode)
            .FirstOrDefaultAsync(n => n.Id == nodeId);
    }

    /// <summary>Returns graph statistics.</summary>
    public async Task<GraphStatistics> GetStatisticsAsync()
    {
        var nodes = await _db.CommonUnderstandingNodes.ToListAsync();
        return new GraphStatistics
        {
            TotalNodes = nodes.Count,
            SettledCount = nodes.Count(n => n.Status == PropositionStatus.Settled),
            ContestedCount = nodes.Count(n => n.Status == PropositionStatus.Contested),
            UnknownCount = nodes.Count(n => n.Status == PropositionStatus.Unknown),
            UnevaluatedCount = nodes.Count(n => n.Status == PropositionStatus.Unevaluated),
            AverageConfidence = nodes.Any() ? nodes.Average(n => n.Confidence) : 0.5,
            TotalEvidenceItems = nodes.Sum(n => n.EvidenceCount)
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task UpsertNodeAsync(Proposition proposition, int argumentId)
    {
        var key = NormalizeKey(proposition.Text);
        if (string.IsNullOrWhiteSpace(key)) return;

        var node = await _db.CommonUnderstandingNodes
            .FirstOrDefaultAsync(n => n.NormalizedKey == key);

        if (node == null)
        {
            // New proposition — create node
            node = new CommonUnderstandingNode
            {
                Text = proposition.Text.Trim(),
                NormalizedKey = key,
                Status = proposition.Status,
                Confidence = proposition.ConfidenceScore,
                EvidenceCount = proposition.EvidenceCount,
                ArgumentIdsJson = JsonSerializer.Serialize(new List<int> { argumentId }),
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            _db.CommonUnderstandingNodes.Add(node);
        }
        else
        {
            // Existing proposition — update with latest confidence and add argument reference
            var argIds = DeserializeIntList(node.ArgumentIdsJson);
            if (!argIds.Contains(argumentId))
            {
                argIds.Add(argumentId);
                node.ArgumentIdsJson = JsonSerializer.Serialize(argIds);
            }

            // Merge confidence (evidence-count weighted average across contributing arguments)
            var oldEvidence = node.EvidenceCount;
            var newEvidence = proposition.EvidenceCount;
            var totalEvidence = oldEvidence + newEvidence;

            if (totalEvidence > 0)
            {
                node.Confidence = Math.Round(
                    (node.Confidence * oldEvidence + proposition.ConfidenceScore * newEvidence) /
                    totalEvidence, 3);
            }

            node.EvidenceCount = Math.Max(node.EvidenceCount, proposition.EvidenceCount);

            // Escalate status (never downgrade settled → unevaluated)
            node.Status = MergeStatus(node.Status, proposition.Status);
            node.LastUpdatedAt = DateTime.UtcNow;
            node.Version++;
        }

        await _db.SaveChangesAsync();
    }

    private static string NormalizeKey(string text)
    {
        // Lowercase, collapse whitespace, strip trailing punctuation
        var normalized = Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
        normalized = normalized.TrimEnd('.', ',', ';', ':', '!', '?');
        return normalized.Length > 500 ? normalized[..500] : normalized;
    }

    private static PropositionStatus MergeStatus(PropositionStatus existing, PropositionStatus incoming)
    {
        // Priority order: Contested > Settled > Unknown > Unevaluated
        static int Priority(PropositionStatus s) => s switch
        {
            PropositionStatus.Contested => 3,
            PropositionStatus.Settled => 2,
            PropositionStatus.Unknown => 1,
            _ => 0
        };

        return Priority(incoming) > Priority(existing) ? incoming : existing;
    }

    private static List<int> DeserializeIntList(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new(); }
        catch { return new(); }
    }
}

public class GraphStatistics
{
    public int TotalNodes { get; set; }
    public int SettledCount { get; set; }
    public int ContestedCount { get; set; }
    public int UnknownCount { get; set; }
    public int UnevaluatedCount { get; set; }
    public double AverageConfidence { get; set; }
    public int TotalEvidenceItems { get; set; }
}
