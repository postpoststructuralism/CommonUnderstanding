using System.Text.Json;
using System.Text.RegularExpressions;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Partial class containing private helper methods for UnderstandingGraphService.
/// </summary>
public partial class UnderstandingGraphService
{
    // ── Node upsert ───────────────────────────────────────────────────────

    private async Task UpsertNodeAsync(ApplicationDbContext db, Proposition proposition, int argumentId)
    {
        var key = NormalizeKey(proposition.Text);
        if (string.IsNullOrWhiteSpace(key)) return;
        var node = await db.UnderstandingNodes.FirstOrDefaultAsync(n => n.NormalizedKey == key);
        if (node == null)
        {
            float[]? embedding = null;
            try { embedding = await _embeddingService.GenerateEmbeddingAsync(proposition.Text); }
            catch { _logger.LogDebug("Embedding generation skipped for proposition."); }

            node = new UnderstandingNode
            {
                CanonicalText = proposition.Text.Trim(),
                NormalizedKey = key,
                Status = proposition.Status,
                Confidence = proposition.ConfidenceScore,
                EvidenceCount = proposition.EvidenceCount,
                SemanticEmbedding = embedding,
                ArgumentIdsJson = JsonSerializer.Serialize(new List<int> { argumentId }),
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.UnderstandingNodes.Add(node);
        }
        else
        {
            var argIds = DeserializeIntList(node.ArgumentIdsJson);
            if (!argIds.Contains(argumentId)) { argIds.Add(argumentId); node.ArgumentIdsJson = JsonSerializer.Serialize(argIds); }
            var oldEv = node.EvidenceCount;
            var newEv = proposition.EvidenceCount;
            var total = oldEv + newEv;
            if (total > 0) node.Confidence = Math.Round((node.Confidence * oldEv + proposition.ConfidenceScore * newEv) / total, 3);
            node.EvidenceCount = Math.Max(node.EvidenceCount, proposition.EvidenceCount);
            node.Status = MergeStatus(node.Status, proposition.Status);
            node.LastUpdatedAt = DateTime.UtcNow;
            node.Version++;
        }
        await db.SaveChangesAsync();
    }

    private async Task UpsertNodeFromSocialPropositionAsync(ApplicationDbContext db, SocialProposition proposition, Guid socialArgumentId)
    {
        var key = NormalizeKey(proposition.Text);
        if (string.IsNullOrWhiteSpace(key)) return;
        var node = await db.UnderstandingNodes.FirstOrDefaultAsync(n => n.NormalizedKey == key);
        if (node == null)
        {
            float[]? embedding = null;
            try { embedding = proposition.Embedding ?? await _embeddingService.GenerateEmbeddingAsync(proposition.Text); }
            catch { _logger.LogDebug("Embedding generation skipped for social proposition."); }

            node = new UnderstandingNode
            {
                CanonicalText = proposition.Text.Trim(),
                NormalizedKey = key,
                Status = PropositionStatus.Unevaluated,
                Confidence = 0.5,
                EvidenceCount = 1,
                SemanticEmbedding = embedding,
                ArgumentIdsJson = JsonSerializer.Serialize(new List<string> { socialArgumentId.ToString() }),
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.UnderstandingNodes.Add(node);
        }
        else
        {
            var argIds = DeserializeGuidList(node.ArgumentIdsJson);
            var idStr = socialArgumentId.ToString();
            if (!argIds.Contains(idStr)) { argIds.Add(idStr); node.ArgumentIdsJson = JsonSerializer.Serialize(argIds); }
            node.LastUpdatedAt = DateTime.UtcNow;
            node.Version++;
        }
        await db.SaveChangesAsync();
    }

    // ── Edge detection helpers ────────────────────────────────────────────

    private async Task DetectEdgesForArgumentAsync(ApplicationDbContext db, Argument argument)
    {
        var propositions = argument.Claims.SelectMany(c => c.Premises).ToList();
        if (propositions.Count < 2) return;
        var keys = propositions.Select(p => NormalizeKey(p.Text)).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        var nodes = await db.UnderstandingNodes.Where(n => keys.Contains(n.NormalizedKey)).ToListAsync();
        int created = 0;
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (await db.UnderstandingEdges.AnyAsync(e =>
                    (e.SourceNodeId == nodes[i].Id && e.TargetNodeId == nodes[j].Id) ||
                    (e.SourceNodeId == nodes[j].Id && e.TargetNodeId == nodes[i].Id))) continue;
                double sim = (nodes[i].SemanticEmbedding != null && nodes[j].SemanticEmbedding != null)
                    ? CosineSimilarity(nodes[i].SemanticEmbedding, nodes[j].SemanticEmbedding) : 0.5;
                db.UnderstandingEdges.Add(new UnderstandingEdge
                {
                    SourceNodeId = nodes[i].Id, TargetNodeId = nodes[j].Id,
                    Relationship = sim > 0.7 ? "supports" : "qualifies",
                    Weight = Math.Round(sim, 4), BaseWeight = Math.Round(sim, 4),
                    ProvenanceJson = JsonSerializer.Serialize(new { detectedBy = "argument_co_occurrence", argumentId = argument.Id }),
                    CreatedAt = DateTime.UtcNow, LastReinforcedAt = DateTime.UtcNow
                });
                created++;
            }
        if (created > 0) await db.SaveChangesAsync();
    }

    private async Task DetectEdgesForSocialArgumentAsync(ApplicationDbContext db, SocialArgument socialArg)
    {
        var props = socialArg.ArgumentPropositions?.Select(ap => ap.Proposition).Where(p => p != null).ToList();
        if (props == null || props.Count < 2) return;
        var keys = props.Select(p => NormalizeKey(p!.Text)).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        var nodes = await db.UnderstandingNodes.Where(n => keys.Contains(n.NormalizedKey)).ToListAsync();
        int created = 0;
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (await db.UnderstandingEdges.AnyAsync(e =>
                    (e.SourceNodeId == nodes[i].Id && e.TargetNodeId == nodes[j].Id) ||
                    (e.SourceNodeId == nodes[j].Id && e.TargetNodeId == nodes[i].Id))) continue;
                double sim = (nodes[i].SemanticEmbedding != null && nodes[j].SemanticEmbedding != null)
                    ? CosineSimilarity(nodes[i].SemanticEmbedding, nodes[j].SemanticEmbedding) : 0.5;
                db.UnderstandingEdges.Add(new UnderstandingEdge
                {
                    SourceNodeId = nodes[i].Id, TargetNodeId = nodes[j].Id,
                    Relationship = sim > 0.7 ? "supports" : "qualifies",
                    Weight = Math.Round(sim, 4), BaseWeight = Math.Round(sim, 4),
                    ProvenanceJson = JsonSerializer.Serialize(new { detectedBy = "social_argument_co_occurrence", socialArgumentId = socialArg.Id }),
                    CreatedAt = DateTime.UtcNow, LastReinforcedAt = DateTime.UtcNow
                });
                created++;
            }
        if (created > 0) await db.SaveChangesAsync();
    }

    // ── Relationship determination ────────────────────────────────────────

    private static string DetermineRelationship(UnderstandingNode a, UnderstandingNode b, double similarity)
    {
        if (similarity >= 0.85) return "supports";
        if (similarity >= 0.65) return "refines";
        if (similarity >= 0.45) return "qualifies";
        return "assumes";
    }

    // ── Status merging ────────────────────────────────────────────────────

    private static PropositionStatus MergeStatus(PropositionStatus current, PropositionStatus incoming)
    {
        // Priority: Contested > Settled > Unknown > Unevaluated
        if (current == PropositionStatus.Contested || incoming == PropositionStatus.Contested)
            return PropositionStatus.Contested;
        if (current == PropositionStatus.Settled || incoming == PropositionStatus.Settled)
            return PropositionStatus.Settled;
        if (current == PropositionStatus.Unknown || incoming == PropositionStatus.Unknown)
            return PropositionStatus.Unknown;
        return PropositionStatus.Unevaluated;
    }

    // ── Text normalization ────────────────────────────────────────────────

    private static string NormalizeKey(string text)
    {
        var normalized = Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
        normalized = normalized.TrimEnd('.', ',', ';', ':', '!', '?');
        return normalized.Length > 500 ? normalized[..500] : normalized;
    }

    // ── Cosine similarity ─────────────────────────────────────────────────

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0.0;
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        if (magA == 0 || magB == 0) return 0.0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    // ── JSON list deserialization ─────────────────────────────────────────

    private static List<int> DeserializeIntList(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>(); }
        catch { return new List<int>(); }
    }

    private static List<string> DeserializeGuidList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }
}