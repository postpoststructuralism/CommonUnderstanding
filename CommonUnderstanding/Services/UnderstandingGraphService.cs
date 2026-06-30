using System.Text.Json;
using System.Text.RegularExpressions;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

public partial class UnderstandingGraphService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly EmbeddingService _embeddingService;
    private readonly ILogger<UnderstandingGraphService> _logger;

    public UnderstandingGraphService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        EmbeddingService embeddingService,
        ILogger<UnderstandingGraphService> logger)
    {
        _contextFactory = contextFactory;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    // ── Sync ──────────────────────────────────────────────────────────────

    public async Task SyncFromArgumentAsync(int argumentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var argument = await db.Arguments.Include(a => a.Claims).ThenInclude(c => c.Premises)
            .FirstOrDefaultAsync(a => a.Id == argumentId);
        if (argument == null) { _logger.LogWarning("Argument {Id} not found.", argumentId); return; }
        _logger.LogInformation("Syncing argument {Id} into Understanding Graph", argumentId);
        foreach (var claim in argument.Claims)
            foreach (var prop in claim.Premises)
                await UpsertNodeAsync(db, prop, argumentId);
        await DetectEdgesForArgumentAsync(db, argument);
    }

    public async Task SyncFromSocialArgumentAsync(Guid socialArgumentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var sa = await db.SocialArguments.Include(x => x.ArgumentPropositions).ThenInclude(ap => ap.Proposition)
            .FirstOrDefaultAsync(x => x.Id == socialArgumentId);
        if (sa == null) { _logger.LogWarning("SocialArgument {Id} not found.", socialArgumentId); return; }
        _logger.LogInformation("Syncing SocialArgument {Id} into Understanding Graph", socialArgumentId);
        foreach (var ap in sa.ArgumentPropositions)
            if (ap.Proposition != null)
                await UpsertNodeFromSocialPropositionAsync(db, ap.Proposition, socialArgumentId);
        await DetectEdgesForSocialArgumentAsync(db, sa);
    }

    /// <summary>
    /// Bulk-syncs ALL existing arguments and social arguments into the Understanding Graph.
    /// This populates the graph from existing data so schema discovery can run.
    /// </summary>
    public async Task SyncAllAsync()
    {
        _logger.LogInformation("Starting bulk sync of all data into Understanding Graph...");

        await using var db = await _contextFactory.CreateDbContextAsync();

        // Sync legacy Arguments
        var arguments = await db.Arguments
            .Include(a => a.Claims).ThenInclude(c => c.Premises)
            .ToListAsync();
        _logger.LogInformation("Syncing {Count} legacy arguments...", arguments.Count);
        foreach (var argument in arguments)
        {
            foreach (var claim in argument.Claims)
                foreach (var prop in claim.Premises)
                    await UpsertNodeAsync(db, prop, argument.Id);
            await DetectEdgesForArgumentAsync(db, argument);
        }

        // Sync SocialArguments
        var socialArgs = await db.SocialArguments
            .Include(x => x.ArgumentPropositions).ThenInclude(ap => ap.Proposition)
            .ToListAsync();
        _logger.LogInformation("Syncing {Count} social arguments...", socialArgs.Count);
        foreach (var sa in socialArgs)
        {
            foreach (var ap in sa.ArgumentPropositions)
                if (ap.Proposition != null)
                    await UpsertNodeFromSocialPropositionAsync(db, ap.Proposition, sa.Id);
            await DetectEdgesForSocialArgumentAsync(db, sa);
        }

        _logger.LogInformation("Bulk sync complete.");
    }

    // ── Edge detection ────────────────────────────────────────────────────

    public async Task DetectEdgesAsync(double similarityThreshold = 0.75)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var nodes = await db.UnderstandingNodes.Where(n => n.SemanticEmbedding != null).ToListAsync();
        _logger.LogInformation("Detecting edges among {Count} nodes (threshold={Threshold})", nodes.Count, similarityThreshold);
        int created = 0;
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var a = nodes[i]; var b = nodes[j];
                if (await db.UnderstandingEdges.AnyAsync(e =>
                    (e.SourceNodeId == a.Id && e.TargetNodeId == b.Id) ||
                    (e.SourceNodeId == b.Id && e.TargetNodeId == a.Id))) continue;
                var aArgs = DeserializeIntList(a.ArgumentIdsJson);
                var bArgs = DeserializeIntList(b.ArgumentIdsJson);
                bool shareContext = aArgs.Intersect(bArgs).Any();
                double sim = (a.SemanticEmbedding != null && b.SemanticEmbedding != null)
                    ? CosineSimilarity(a.SemanticEmbedding, b.SemanticEmbedding) : 0;
                if (sim >= similarityThreshold || shareContext)
                {
                    var rel = DetermineRelationship(a, b, sim);
                    var w = shareContext ? Math.Min(1.0, sim + 0.1) : sim;
                    db.UnderstandingEdges.Add(new UnderstandingEdge
                    {
                        SourceNodeId = a.Id, TargetNodeId = b.Id, Relationship = rel,
                        Weight = Math.Round(w, 4), BaseWeight = Math.Round(w, 4),
                        ProvenanceJson = JsonSerializer.Serialize(new
                        {
                            detectedBy = shareContext ? "co-occurrence" : "semantic_similarity",
                            similarity = Math.Round(sim, 4),
                            sharedArgumentIds = shareContext ? aArgs.Intersect(bArgs).ToList() : null
                        }),
                        CreatedAt = DateTime.UtcNow, LastReinforcedAt = DateTime.UtcNow
                    });
                    created++;
                }
            }
        _logger.LogInformation("Edge detection complete: {Count} edges created.", created);
        await db.SaveChangesAsync();
    }

    // ── Topology metrics ──────────────────────────────────────────────────

    public async Task RecomputeTopologyMetricsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var nodes = await db.UnderstandingNodes.ToListAsync();
        var edges = await db.UnderstandingEdges.ToListAsync();
        if (nodes.Count == 0) return;
        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var n in nodes) adj[n.Id] = new HashSet<int>();
        foreach (var e in edges)
            if (adj.ContainsKey(e.SourceNodeId) && adj.ContainsKey(e.TargetNodeId))
            { adj[e.SourceNodeId].Add(e.TargetNodeId); adj[e.TargetNodeId].Add(e.SourceNodeId); }
        int nCount = nodes.Count;
        var idList = nodes.Select(n => n.Id).ToList();
        var idIndex = idList.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        foreach (var node in nodes)
            node.DegreeCentrality = nCount > 1 ? (double)adj[node.Id].Count / (nCount - 1) : 0;

        var betweenness = new Dictionary<int, double>();
        foreach (var id in idList) betweenness[id] = 0;
        foreach (var s in idList)
        {
            var stack = new Stack<int>();
            var pred = new Dictionary<int, List<int>>();
            var sigma = new Dictionary<int, double>();
            var dist = new Dictionary<int, double>();
            var delta = new Dictionary<int, double>();
            foreach (var t in idList) { pred[t] = new List<int>(); sigma[t] = 0; dist[t] = -1; delta[t] = 0; }
            sigma[s] = 1; dist[s] = 0;
            var q = new Queue<int>(); q.Enqueue(s);
            while (q.Count > 0)
            {
                var v = q.Dequeue(); stack.Push(v);
                foreach (var w in adj[v])
                {
                    if (dist[w] < 0) { dist[w] = dist[v] + 1; q.Enqueue(w); }
                    if (Math.Abs(dist[w] - (dist[v] + 1)) < 1e-9) { sigma[w] += sigma[v]; pred[w].Add(v); }
                }
            }
            while (stack.Count > 0)
            {
                var w = stack.Pop();
                foreach (var v in pred[w]) delta[v] += (sigma[v] / sigma[w]) * (1 + delta[w]);
                if (w != s) betweenness[w] += delta[w];
            }
        }
        double maxBetw = betweenness.Values.Max();
        foreach (var node in nodes)
            node.BetweennessCentrality = maxBetw > 0 ? betweenness[node.Id] / maxBetw : 0;

        foreach (var node in nodes)
        {
            var nb = adj[node.Id].ToList(); int k = nb.Count;
            if (k < 2) { node.ClusteringCoefficient = 0; continue; }
            int tri = 0;
            for (int i = 0; i < k; i++) for (int j = i + 1; j < k; j++) if (adj[nb[i]].Contains(nb[j])) tri++;
            node.ClusteringCoefficient = (double)(2 * tri) / (k * (k - 1));
        }

        double damp = 0.85;
        var pr = new Dictionary<int, double>();
        foreach (var id in idList) pr[id] = 1.0 / nCount;
        for (int iter = 0; iter < 30; iter++)
        {
            var npr = new Dictionary<int, double>();
            double dSum = 0;
            foreach (var id in idList) if (adj[id].Count == 0) dSum += pr[id];
            foreach (var id in idList)
            {
                double sum = 0;
                foreach (var nid in idList) if (adj[nid].Contains(id)) sum += pr[nid] / adj[nid].Count;
                npr[id] = (1.0 - damp) / nCount + damp * (sum + dSum / nCount);
            }
            pr = npr;
        }
        foreach (var node in nodes) node.PageRank = pr[node.Id];

        foreach (var node in nodes)
            node.ControversyScore = Math.Round((node.PageRank * 2 + node.BetweennessCentrality + (1 - node.ClusteringCoefficient)) / 4, 4);

        foreach (var node in nodes)
        {
            var ne = edges.Where(e => e.SourceNodeId == node.Id || e.TargetNodeId == node.Id).ToList();
            if (ne.Count < 2) { node.DialecticalTemperature = 0; continue; }
            var rc = ne.GroupBy(e => e.Relationship).ToDictionary(g => g.Key, g => g.Count());
            double tot = ne.Count, ent = 0;
            foreach (var c in rc.Values) { double p = c / tot; ent -= p * Math.Log(p, 2); }
            node.DialecticalTemperature = Math.Round(ent / Math.Log(rc.Count + 1, 2), 4);
        }

        var adjM = Matrix<double>.Build.Dense(nCount, nCount, 0);
        foreach (var e in edges)
            if (idIndex.TryGetValue(e.SourceNodeId, out int si) && idIndex.TryGetValue(e.TargetNodeId, out int ti))
            { adjM[si, ti] = e.Weight; adjM[ti, si] = e.Weight; }
        var ev = Vector<double>.Build.Dense(nCount, 1.0 / Math.Sqrt(nCount));
        for (int iter = 0; iter < 20; iter++) { ev = adjM * ev; double norm = ev.L2Norm(); if (norm > 1e-12) ev /= norm; }
        for (int i = 0; i < nCount; i++) nodes[i].EigenvectorCentrality = Math.Round(Math.Abs(ev[i]), 6);

        foreach (var node in nodes)
        {
            var mems = await db.SchemaMemberships.Where(m => m.NodeId == node.Id).ToListAsync();
            if (mems.Count < 2) { node.SchemaEntropy = 0; continue; }
            double tw = mems.Sum(m => m.Weight), en = 0;
            foreach (var m in mems) { double p = m.Weight / tw; en -= p * Math.Log(p, 2); }
            node.SchemaEntropy = Math.Round(en / Math.Log(mems.Count, 2), 4);
        }
        await db.SaveChangesAsync();
        _logger.LogInformation("Topology metrics recomputed for {Count} nodes.", nodes.Count);
    }

    // ── Migration ─────────────────────────────────────────────────────────

    public async Task<int> MigrateFromLegacyAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var legacyNodes = await db.CommonUnderstandingNodes.ToListAsync();
        var legacyEdges = await db.CommonUnderstandingEdges.Include(e => e.SourceNode).Include(e => e.TargetNode).ToListAsync();
        int nMig = 0, eMig = 0;
        foreach (var l in legacyNodes)
        {
            if (await db.UnderstandingNodes.AnyAsync(n => n.NormalizedKey == l.NormalizedKey)) continue;
            db.UnderstandingNodes.Add(new UnderstandingNode
            {
                CanonicalText = l.Text, NormalizedKey = l.NormalizedKey, Status = l.Status,
                Confidence = l.Confidence, EvidenceCount = l.EvidenceCount, ArgumentIdsJson = l.ArgumentIdsJson,
                Version = l.Version, FirstSeenAt = l.FirstSeenAt, LastUpdatedAt = l.LastUpdatedAt
            });
            nMig++;
        }
        await db.SaveChangesAsync();
        foreach (var l in legacyEdges)
        {
            var src = await db.UnderstandingNodes.FirstOrDefaultAsync(n => n.NormalizedKey == l.SourceNode!.NormalizedKey);
            var tgt = await db.UnderstandingNodes.FirstOrDefaultAsync(n => n.NormalizedKey == l.TargetNode!.NormalizedKey);
            if (src == null || tgt == null) continue;
            if (await db.UnderstandingEdges.AnyAsync(e => e.SourceNodeId == src.Id && e.TargetNodeId == tgt.Id && e.Relationship == l.Relationship)) continue;
            db.UnderstandingEdges.Add(new UnderstandingEdge
            {
                SourceNodeId = src.Id, TargetNodeId = tgt.Id, Relationship = l.Relationship,
                Weight = l.Strength, BaseWeight = l.Strength,
                ProvenanceJson = JsonSerializer.Serialize(new { migratedFrom = "CommonUnderstandingEdge", legacyEdgeId = l.Id }),
                CreatedAt = l.CreatedAt, LastReinforcedAt = l.CreatedAt
            });
            eMig++;
        }
        await db.SaveChangesAsync();
        _logger.LogInformation("Migration: {Nodes} nodes, {Edges} edges.", nMig, eMig);
        return nMig + eMig;
    }

    // ── Query ─────────────────────────────────────────────────────────────

    public async Task<List<UnderstandingNode>> GetAllNodesAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UnderstandingNodes.OrderByDescending(n => n.Confidence).ToListAsync();
    }

    public async Task<UnderstandingNode?> GetNodeWithEdgesAsync(int nodeId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UnderstandingNodes.Include(n => n.OutboundEdges).ThenInclude(e => e.TargetNode)
            .Include(n => n.InboundEdges).ThenInclude(e => e.SourceNode)
            .Include(n => n.SchemaMemberships).ThenInclude(m => m.Schema)
            .FirstOrDefaultAsync(n => n.Id == nodeId);
    }

    public async Task<List<UnderstandingNode>> SearchNodesAsync(string query)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        if (string.IsNullOrWhiteSpace(query)) return await GetAllNodesAsync();
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var all = await db.UnderstandingNodes.ToListAsync();
        return all.Where(n => terms.Any(t => n.CanonicalText.Contains(t, StringComparison.OrdinalIgnoreCase)))
                  .OrderByDescending(n => n.Confidence).ToList();
    }

    public async Task<GraphStatistics> GetStatisticsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var nodes = await db.UnderstandingNodes.ToListAsync();
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

    // ── Private helpers (see UnderstandingGraphService.Helpers.cs) ─────────
}