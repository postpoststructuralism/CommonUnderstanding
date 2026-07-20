using System.Text.Json;
using System.Text.RegularExpressions;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        // Sync legacy Arguments — use projection to avoid loading full entity graphs
        var argProjections = await db.Arguments
            .Select(a => new
            {
                a.Id,
                Premises = a.Claims.SelectMany(c => c.Premises.Select(p => new
                {
                    p.Text,
                    p.Status,
                    p.ConfidenceScore,
                    p.EvidenceCount
                }))
            })
            .ToListAsync();
        _logger.LogInformation("Syncing {Count} legacy arguments...", argProjections.Count);
        foreach (var arg in argProjections)
        {
            foreach (var p in arg.Premises)
            {
                // Create lightweight Proposition-like object for UpsertNodeAsync
                var prop = new Proposition
                {
                    Text = p.Text,
                    Status = p.Status,
                    ConfidenceScore = p.ConfidenceScore,
                    EvidenceCount = p.EvidenceCount
                };
                await UpsertNodeAsync(db, prop, arg.Id);
            }
            // DetectEdgesForArgumentAsync needs the full Argument for Claims→Premises navigation
            // Load it on-demand (only needed when ≥2 premises exist)
            if (arg.Premises.Count() >= 2)
            {
                var argument = await db.Arguments
                    .Include(a => a.Claims).ThenInclude(c => c.Premises)
                    .FirstOrDefaultAsync(a => a.Id == arg.Id);
                if (argument != null)
                    await DetectEdgesForArgumentAsync(db, argument);
            }
        }

        // Sync SocialArguments — use projection to avoid loading full entity graphs
        var saProjections = await db.SocialArguments
            .Select(sa => new
            {
                sa.Id,
                Propositions = sa.ArgumentPropositions
                    .Where(ap => ap.Proposition != null)
                    .Select(ap => new
                    {
                        ap.Proposition!.Text,
                        ap.Proposition.Embedding
                    })
            })
            .ToListAsync();
        _logger.LogInformation("Syncing {Count} social arguments...", saProjections.Count);
        foreach (var sa in saProjections)
        {
            foreach (var p in sa.Propositions)
            {
                var prop = new SocialProposition
                {
                    Text = p.Text,
                    Embedding = p.Embedding
                };
                await UpsertNodeFromSocialPropositionAsync(db, prop, sa.Id);
            }
            // DetectEdgesForSocialArgumentAsync needs full navigation — load on-demand
            if (sa.Propositions.Count() >= 2)
            {
                var socialArg = await db.SocialArguments
                    .Include(x => x.ArgumentPropositions).ThenInclude(ap => ap.Proposition)
                    .FirstOrDefaultAsync(x => x.Id == sa.Id);
                if (socialArg != null)
                    await DetectEdgesForSocialArgumentAsync(db, socialArg);
            }
        }

        _logger.LogInformation("Bulk sync complete.");
    }

    // ── Edge detection ────────────────────────────────────────────────────

    public async Task DetectEdgesAsync(double similarityThreshold = 0.75)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Use projection to load ONLY the columns needed for the O(n²) loop.
        // Avoids loading CanonicalText, GraphEmbedding, SchwartzVector, MoralFoundationsVector,
        // DimensionalCoordinatesJson, and other heavy columns.
        var nodeProjections = await db.UnderstandingNodes
            .Where(n => n.SemanticEmbedding != null)
            .Select(n => new
            {
                n.Id,
                n.SemanticEmbedding,
                n.ArgumentIdsJson,
                n.Status
            })
            .ToListAsync();

        // Pre-load ALL existing edge pairs into a HashSet for O(1) lookup.
        // This eliminates the per-pair AnyAsync DB round-trip (was ~50M queries for 10K nodes).
        var existingPairs = await db.UnderstandingEdges
            .Select(e => new { e.SourceNodeId, e.TargetNodeId })
            .ToListAsync();
        var edgeSet = new HashSet<(int, int)>();
        foreach (var e in existingPairs)
        {
            edgeSet.Add((e.SourceNodeId, e.TargetNodeId));
            edgeSet.Add((e.TargetNodeId, e.SourceNodeId)); // undirected
        }

        _logger.LogInformation("Detecting edges among {Count} nodes (threshold={Threshold})",
            nodeProjections.Count, similarityThreshold);
        int created = 0;
        for (int i = 0; i < nodeProjections.Count; i++)
            for (int j = i + 1; j < nodeProjections.Count; j++)
            {
                var a = nodeProjections[i]; var b = nodeProjections[j];
                if (edgeSet.Contains((a.Id, b.Id))) continue;
                var aArgs = DeserializeIntList(a.ArgumentIdsJson);
                var bArgs = DeserializeIntList(b.ArgumentIdsJson);
                bool shareContext = aArgs.Intersect(bArgs).Any();
                double sim = (a.SemanticEmbedding != null && b.SemanticEmbedding != null)
                    ? CosineSimilarity(a.SemanticEmbedding, b.SemanticEmbedding) : 0;
                if (sim >= similarityThreshold || shareContext)
                {
                    // DetermineRelationship needs Status and ArgumentIdsJson (already in projection)
                    var rel = DetermineRelationshipFromProjection(a.Id, a.Status, a.ArgumentIdsJson,
                        b.Id, b.Status, b.ArgumentIdsJson, sim);
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

    /// <summary>
    /// Lightweight version of DetermineRelationship that works with projections
    /// instead of full UnderstandingNode entities.
    /// </summary>
    private static string DetermineRelationshipFromProjection(
        int nodeAId, PropositionStatus statusA, string? argsJsonA,
        int nodeBId, PropositionStatus statusB, string? argsJsonB,
        double similarity)
    {
        if (similarity >= 0.85) return "supports";
        if (similarity >= 0.65) return "refines";
        if (similarity >= 0.45) return "qualifies";

        // Low similarity but shared argument context → likely contradiction
        if (similarity < 0.30)
        {
            var aArgs = DeserializeIntList(argsJsonA);
            var bArgs = DeserializeIntList(argsJsonB);
            if (aArgs.Intersect(bArgs).Any())
                return "contradicts";
        }

        // One contested, one settled with moderate-low similarity → contradiction signal
        if (similarity < 0.55 &&
            ((statusA == PropositionStatus.Contested && statusB == PropositionStatus.Settled) ||
             (statusA == PropositionStatus.Settled && statusB == PropositionStatus.Contested)))
        {
            var aArgs = DeserializeIntList(argsJsonA);
            var bArgs = DeserializeIntList(argsJsonB);
            if (aArgs.Intersect(bArgs).Any())
                return "contradicts";
        }

        return "assumes";
    }

    /// <summary>
    /// Scans for additional contradiction signals that pure semantic similarity
    /// may miss. Uses three strategies:
    ///
    /// 1. Evidence direction: If two nodes have evidence items pointing in
    ///    opposite directions (Supports vs Opposes), they contradict.
    /// 2. Social argument links: If the source SocialArguments of two nodes
    ///    are linked with a Contradicts relationship, the nodes contradict.
    /// 3. Rebuttal propositions: If a proposition was added as a Rebuttal type
    ///    in a social argument, it likely contradicts the argument's claim.
    /// </summary>
    public async Task<int> DetectContradictionsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        int created = 0;

        // Pre-load edge set for O(1) existence checks (shared across all strategies)
        var existingPairs = await db.UnderstandingEdges
            .Select(e => new { e.SourceNodeId, e.TargetNodeId, e.Relationship })
            .ToListAsync();
        var contradictionSet = new HashSet<(int, int)>();
        var anyEdgeSet = new HashSet<(int, int)>();
        foreach (var e in existingPairs)
        {
            var pair = (Math.Min(e.SourceNodeId, e.TargetNodeId), Math.Max(e.SourceNodeId, e.TargetNodeId));
            anyEdgeSet.Add(pair);
            if (e.Relationship == "contradicts")
                contradictionSet.Add(pair);
        }

        // ── Strategy 1: Evidence direction ────────────────────────────────
        // Use projection to load only needed columns (no embeddings)
        var nodeProjections = await db.UnderstandingNodes
            .Where(n => n.SemanticEmbedding != null)
            .Select(n => new
            {
                n.Id,
                n.ArgumentIdsJson
            })
            .ToListAsync();

        // Pre-load all evidence directions in a single query to avoid N+1
        var evidenceDirections = await db.EvidenceItems
            .Select(ei => new { ei.Proposition.Claim.ArgumentId, ei.Direction })
            .Distinct()
            .ToListAsync();
        var directionsByArg = evidenceDirections
            .GroupBy(e => e.ArgumentId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Direction).ToHashSet());

        for (int i = 0; i < nodeProjections.Count; i++)
        {
            for (int j = i + 1; j < nodeProjections.Count; j++)
            {
                var a = nodeProjections[i];
                var b = nodeProjections[j];

                var pairKey = (Math.Min(a.Id, b.Id), Math.Max(a.Id, b.Id));
                if (contradictionSet.Contains(pairKey))
                    continue;

                var aArgs = DeserializeIntList(a.ArgumentIdsJson);
                var bArgs = DeserializeIntList(b.ArgumentIdsJson);
                var sharedArgs = aArgs.Intersect(bArgs).ToList();
                if (sharedArgs.Count == 0) continue;

                // Check evidence direction via pre-loaded dictionary (O(1) instead of N+1 DB query)
                foreach (var argId in sharedArgs)
                {
                    if (!directionsByArg.TryGetValue(argId, out var directions)) continue;
                    bool hasSupports = directions.Contains(EvidenceDirection.Supports);
                    bool hasOpposes = directions.Contains(EvidenceDirection.Opposes);

                    if (hasSupports && hasOpposes)
                    {
                        // Remove any existing non-contradiction edge between these nodes
                        if (anyEdgeSet.Contains(pairKey))
                        {
                            var existingEdge = await db.UnderstandingEdges
                                .FirstOrDefaultAsync(e =>
                                    (e.SourceNodeId == a.Id && e.TargetNodeId == b.Id) ||
                                    (e.SourceNodeId == b.Id && e.TargetNodeId == a.Id));
                            if (existingEdge != null)
                                db.UnderstandingEdges.Remove(existingEdge);
                        }

                        db.UnderstandingEdges.Add(new UnderstandingEdge
                        {
                            SourceNodeId = a.Id,
                            TargetNodeId = b.Id,
                            Relationship = "contradicts",
                            Weight = 0.6,
                            BaseWeight = 0.6,
                            ProvenanceJson = JsonSerializer.Serialize(new
                            {
                                detectedBy = "evidence_direction",
                                argumentId = argId,
                                note = "Nodes share an argument with opposing evidence directions"
                            }),
                            CreatedAt = DateTime.UtcNow,
                            LastReinforcedAt = DateTime.UtcNow
                        });
                        created++;
                        contradictionSet.Add(pairKey);
                        anyEdgeSet.Add(pairKey);
                        break;
                    }
                }
            }
        }

        // ── Strategy 2: Social argument Contradicts links ─────────────────
        // Use projection to avoid loading full entity graphs
        var contradictLinkProjections = await db.Set<CommonUnderstanding.Models.Social.ArgumentLink>()
            .Where(al => al.LinkType == CommonUnderstanding.Models.Social.LinkType.Contradicts)
            .Select(al => new
            {
                al.Id,
                al.SourceArgumentId,
                al.TargetArgumentId,
                SourcePropTexts = al.SourceArgument.ArgumentPropositions
                    .Where(ap => ap.Proposition != null)
                    .Select(ap => ap.Proposition!.Text),
                TargetPropTexts = al.TargetArgument.ArgumentPropositions
                    .Where(ap => ap.Proposition != null)
                    .Select(ap => ap.Proposition!.Text)
            })
            .ToListAsync();

        // Pre-load all relevant UnderstandingNode keys for batch lookup
        var allPropTexts = contradictLinkProjections
            .SelectMany(l => l.SourcePropTexts.Concat(l.TargetPropTexts))
            .Select(t => NormalizeKey(t))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .ToList();

        var nodeLookup = allPropTexts.Count > 0
            ? await db.UnderstandingNodes
                .Where(n => allPropTexts.Contains(n.NormalizedKey))
                .Select(n => new { n.Id, n.NormalizedKey })
                .ToDictionaryAsync(n => n.NormalizedKey, n => n.Id)
            : new Dictionary<string, int>();

        foreach (var link in contradictLinkProjections)
        {
            foreach (var spText in link.SourcePropTexts)
            {
                var sourceKey = NormalizeKey(spText);
                if (string.IsNullOrWhiteSpace(sourceKey) || !nodeLookup.TryGetValue(sourceKey, out int sourceNodeId))
                    continue;

                foreach (var tpText in link.TargetPropTexts)
                {
                    var targetKey = NormalizeKey(tpText);
                    if (string.IsNullOrWhiteSpace(targetKey) || !nodeLookup.TryGetValue(targetKey, out int targetNodeId))
                        continue;

                    var pairKey = (Math.Min(sourceNodeId, targetNodeId), Math.Max(sourceNodeId, targetNodeId));
                    if (contradictionSet.Contains(pairKey))
                        continue;

                    if (anyEdgeSet.Contains(pairKey))
                    {
                        var existingEdge2 = await db.UnderstandingEdges
                            .FirstOrDefaultAsync(e =>
                                (e.SourceNodeId == sourceNodeId && e.TargetNodeId == targetNodeId) ||
                                (e.SourceNodeId == targetNodeId && e.TargetNodeId == sourceNodeId));
                        if (existingEdge2 != null)
                            db.UnderstandingEdges.Remove(existingEdge2);
                    }

                    db.UnderstandingEdges.Add(new UnderstandingEdge
                    {
                        SourceNodeId = sourceNodeId,
                        TargetNodeId = targetNodeId,
                        Relationship = "contradicts",
                        Weight = 0.75,
                        BaseWeight = 0.75,
                        ProvenanceJson = JsonSerializer.Serialize(new
                        {
                            detectedBy = "social_argument_link",
                            sourceArgumentId = link.SourceArgumentId.ToString(),
                            targetArgumentId = link.TargetArgumentId.ToString(),
                            linkId = link.Id.ToString()
                        }),
                        CreatedAt = DateTime.UtcNow,
                        LastReinforcedAt = DateTime.UtcNow
                    });
                    created++;
                    contradictionSet.Add(pairKey);
                    anyEdgeSet.Add(pairKey);
                }
            }
        }

        // ── Strategy 3: Rebuttal propositions ─────────────────────────────
        var rebuttalProjections = await db.Set<CommonUnderstanding.Models.Social.SocialArgumentProposition>()
            .Where(ap => ap.Role == CommonUnderstanding.Models.Social.SocialPropositionType.Rebuttal)
            .Select(ap => new
            {
                ap.ArgumentId,
                RebuttalText = ap.Proposition.Text,
                ClaimText = ap.Argument.ClaimProposition.Text
            })
            .ToListAsync();

        // Batch lookup for rebuttal and claim nodes
        var rebuttalTexts = rebuttalProjections
            .SelectMany(r => new[] { r.RebuttalText, r.ClaimText })
            .Select(t => NormalizeKey(t))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .ToList();

        var rebuttalNodeLookup = rebuttalTexts.Count > 0
            ? await db.UnderstandingNodes
                .Where(n => rebuttalTexts.Contains(n.NormalizedKey))
                .Select(n => new { n.Id, n.NormalizedKey })
                .ToDictionaryAsync(n => n.NormalizedKey, n => n.Id)
            : new Dictionary<string, int>();

        foreach (var rp in rebuttalProjections)
        {
            var rebuttalKey = NormalizeKey(rp.RebuttalText);
            var claimKey = NormalizeKey(rp.ClaimText);
            if (string.IsNullOrWhiteSpace(rebuttalKey) || string.IsNullOrWhiteSpace(claimKey))
                continue;
            if (!rebuttalNodeLookup.TryGetValue(rebuttalKey, out int rebuttalNodeId))
                continue;
            if (!rebuttalNodeLookup.TryGetValue(claimKey, out int claimNodeId))
                continue;

            var pairKey = (Math.Min(rebuttalNodeId, claimNodeId), Math.Max(rebuttalNodeId, claimNodeId));
            if (contradictionSet.Contains(pairKey))
                continue;

            if (anyEdgeSet.Contains(pairKey))
            {
                var existingEdge3 = await db.UnderstandingEdges
                    .FirstOrDefaultAsync(e =>
                        (e.SourceNodeId == rebuttalNodeId && e.TargetNodeId == claimNodeId) ||
                        (e.SourceNodeId == claimNodeId && e.TargetNodeId == rebuttalNodeId));
                if (existingEdge3 != null)
                    db.UnderstandingEdges.Remove(existingEdge3);
            }

            db.UnderstandingEdges.Add(new UnderstandingEdge
            {
                SourceNodeId = rebuttalNodeId,
                TargetNodeId = claimNodeId,
                Relationship = "contradicts",
                Weight = 0.8,
                BaseWeight = 0.8,
                ProvenanceJson = JsonSerializer.Serialize(new
                {
                    detectedBy = "rebuttal_proposition",
                    socialArgumentId = rp.ArgumentId.ToString(),
                    note = "Rebuttal proposition contradicts the argument's claim"
                }),
                CreatedAt = DateTime.UtcNow,
                LastReinforcedAt = DateTime.UtcNow
            });
            created++;
            contradictionSet.Add(pairKey);
            anyEdgeSet.Add(pairKey);
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Contradiction detection complete: {Count} contradiction edges created.", created);
        return created;
    }

    // ── Topology metrics ──────────────────────────────────────────────────

    public async Task RecomputeTopologyMetricsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Use projections to load ONLY the columns needed for topology computation.
        // Avoids loading SemanticEmbedding (~6KB/row), GraphEmbedding, SchwartzVector,
        // MoralFoundationsVector, CanonicalText, and other heavy columns.
        var nodeIds = await db.UnderstandingNodes
            .Select(n => n.Id)
            .ToListAsync();

        var edgeProjections = await db.UnderstandingEdges
            .Select(e => new { e.SourceNodeId, e.TargetNodeId, e.Weight, e.Relationship })
            .ToListAsync();

        if (nodeIds.Count == 0) return;

        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var id in nodeIds) adj[id] = new HashSet<int>();
        foreach (var e in edgeProjections)
            if (adj.ContainsKey(e.SourceNodeId) && adj.ContainsKey(e.TargetNodeId))
            { adj[e.SourceNodeId].Add(e.TargetNodeId); adj[e.TargetNodeId].Add(e.SourceNodeId); }

        int nCount = nodeIds.Count;
        var idIndex = nodeIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        // Degree centrality
        var degreeCentrality = new Dictionary<int, double>();
        foreach (var id in nodeIds)
            degreeCentrality[id] = nCount > 1 ? (double)adj[id].Count / (nCount - 1) : 0;

        // Betweenness centrality (Brandes algorithm)
        var betweenness = new Dictionary<int, double>();
        foreach (var id in nodeIds) betweenness[id] = 0;
        foreach (var s in nodeIds)
        {
            var stack = new Stack<int>();
            var pred = new Dictionary<int, List<int>>();
            var sigma = new Dictionary<int, double>();
            var dist = new Dictionary<int, double>();
            var delta = new Dictionary<int, double>();
            foreach (var t in nodeIds) { pred[t] = new List<int>(); sigma[t] = 0; dist[t] = -1; delta[t] = 0; }
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
        var betweennessCentrality = new Dictionary<int, double>();
        foreach (var id in nodeIds)
            betweennessCentrality[id] = maxBetw > 0 ? betweenness[id] / maxBetw : 0;

        // Clustering coefficient
        var clusteringCoefficient = new Dictionary<int, double>();
        foreach (var id in nodeIds)
        {
            var nb = adj[id].ToList(); int k = nb.Count;
            if (k < 2) { clusteringCoefficient[id] = 0; continue; }
            int tri = 0;
            for (int i = 0; i < k; i++) for (int j = i + 1; j < k; j++) if (adj[nb[i]].Contains(nb[j])) tri++;
            clusteringCoefficient[id] = (double)(2 * tri) / (k * (k - 1));
        }

        // PageRank
        double damp = 0.85;
        var pr = new Dictionary<int, double>();
        foreach (var id in nodeIds) pr[id] = 1.0 / nCount;
        for (int iter = 0; iter < 30; iter++)
        {
            var npr = new Dictionary<int, double>();
            double dSum = 0;
            foreach (var id in nodeIds) if (adj[id].Count == 0) dSum += pr[id];
            foreach (var id in nodeIds)
            {
                double sum = 0;
                foreach (var nid in nodeIds) if (adj[nid].Contains(id)) sum += pr[nid] / adj[nid].Count;
                npr[id] = (1.0 - damp) / nCount + damp * (sum + dSum / nCount);
            }
            pr = npr;
        }

        // Controversy score
        var controversyScore = new Dictionary<int, double>();
        foreach (var id in nodeIds)
            controversyScore[id] = Math.Round((pr[id] * 2 + betweennessCentrality[id] + (1 - clusteringCoefficient[id])) / 4, 4);

        // Dialectical temperature
        var dialecticalTemperature = new Dictionary<int, double>();
        foreach (var id in nodeIds)
        {
            var ne = edgeProjections.Where(e => e.SourceNodeId == id || e.TargetNodeId == id).ToList();
            if (ne.Count < 2) { dialecticalTemperature[id] = 0; continue; }
            var rc = ne.GroupBy(e => e.Relationship).ToDictionary(g => g.Key, g => g.Count());
            double tot = ne.Count, ent = 0;
            foreach (var c in rc.Values) { double p = c / tot; ent -= p * Math.Log(p, 2); }
            dialecticalTemperature[id] = Math.Round(ent / Math.Log(rc.Count + 1, 2), 4);
        }

        // Eigenvector centrality
        var adjM = Matrix<double>.Build.Dense(nCount, nCount, 0);
        foreach (var e in edgeProjections)
            if (idIndex.TryGetValue(e.SourceNodeId, out int si) && idIndex.TryGetValue(e.TargetNodeId, out int ti))
            { adjM[si, ti] = e.Weight; adjM[ti, si] = e.Weight; }
        var ev = Vector<double>.Build.Dense(nCount, 1.0 / Math.Sqrt(nCount));
        for (int iter = 0; iter < 20; iter++) { ev = adjM * ev; double norm = ev.L2Norm(); if (norm > 1e-12) ev /= norm; }
        var eigenvectorCentrality = new Dictionary<int, double>();
        for (int i = 0; i < nCount; i++) eigenvectorCentrality[nodeIds[i]] = Math.Round(Math.Abs(ev[i]), 6);

        // Schema entropy (still needs schema memberships, but those are lightweight)
        var schemaEntropy = new Dictionary<int, double>();
        var allMemberships = await db.SchemaMemberships
            .Select(m => new { m.NodeId, m.Weight })
            .ToListAsync();
        var membershipByNode = allMemberships.GroupBy(m => m.NodeId)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var id in nodeIds)
        {
            if (!membershipByNode.TryGetValue(id, out var mems) || mems.Count < 2)
            { schemaEntropy[id] = 0; continue; }
            double tw = mems.Sum(m => m.Weight), en = 0;
            foreach (var m in mems) { double p = m.Weight / tw; en -= p * Math.Log(p, 2); }
            schemaEntropy[id] = Math.Round(en / Math.Log(mems.Count, 2), 4);
        }

        // Bulk update via raw SQL to avoid loading full entities
        // Use a single UPDATE per metric for efficiency
        var updateSql = @"
            UPDATE ""UnderstandingNodes""
            SET ""DegreeCentrality"" = c.""DegreeCentrality"",
                ""BetweennessCentrality"" = c.""BetweennessCentrality"",
                ""ClusteringCoefficient"" = c.""ClusteringCoefficient"",
                ""PageRank"" = c.""PageRank"",
                ""ControversyScore"" = c.""ControversyScore"",
                ""DialecticalTemperature"" = c.""DialecticalTemperature"",
                ""EigenvectorCentrality"" = c.""EigenvectorCentrality"",
                ""SchemaEntropy"" = c.""SchemaEntropy""
            FROM (VALUES {0}) AS c(""Id"", ""DegreeCentrality"", ""BetweennessCentrality"", ""ClusteringCoefficient"",
                ""PageRank"", ""ControversyScore"", ""DialecticalTemperature"", ""EigenvectorCentrality"", ""SchemaEntropy"")
            WHERE ""UnderstandingNodes"".""Id"" = c.""Id""";

        // Build VALUES clause in batches of 500 to avoid parameter limits
        const int batchSize = 500;
        for (int batch = 0; batch < nodeIds.Count; batch += batchSize)
        {
            var batchIds = nodeIds.Skip(batch).Take(batchSize).ToList();
            var valuesList = new List<string>();
            var parameters = new List<Npgsql.NpgsqlParameter>();

            for (int i = 0; i < batchIds.Count; i++)
            {
                var id = batchIds[i];
                var baseIdx = i * 9;
                valuesList.Add($"(@p{baseIdx}, @p{baseIdx + 1}, @p{baseIdx + 2}, @p{baseIdx + 3}, @p{baseIdx + 4}, @p{baseIdx + 5}, @p{baseIdx + 6}, @p{baseIdx + 7}, @p{baseIdx + 8})");
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx}", id));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 1}", degreeCentrality[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 2}", betweennessCentrality[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 3}", clusteringCoefficient[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 4}", pr[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 5}", controversyScore[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 6}", dialecticalTemperature[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 7}", eigenvectorCentrality[id]));
                parameters.Add(new Npgsql.NpgsqlParameter($"@p{baseIdx + 8}", schemaEntropy[id]));
            }

            var formattedSql = string.Format(updateSql, string.Join(", ", valuesList));
            await db.Database.ExecuteSqlRawAsync(formattedSql, parameters);
        }

        _logger.LogInformation("Topology metrics recomputed for {Count} nodes (projection-based, no heavy column load).", nodeIds.Count);
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
        // Exclude heavy embedding columns — they're not needed for list views
        return await db.UnderstandingNodes
            .AsNoTracking()
            .OrderByDescending(n => n.Confidence)
            .Select(n => new UnderstandingNode
            {
                Id = n.Id,
                CanonicalText = n.CanonicalText,
                NormalizedKey = n.NormalizedKey,
                Status = n.Status,
                Confidence = n.Confidence,
                EvidenceCount = n.EvidenceCount,
                DegreeCentrality = n.DegreeCentrality,
                BetweennessCentrality = n.BetweennessCentrality,
                EigenvectorCentrality = n.EigenvectorCentrality,
                PageRank = n.PageRank,
                ClusteringCoefficient = n.ClusteringCoefficient,
                ControversyScore = n.ControversyScore,
                DialecticalTemperature = n.DialecticalTemperature,
                SchemaEntropy = n.SchemaEntropy,
                ArgumentIdsJson = n.ArgumentIdsJson,
                UserIdsJson = n.UserIdsJson,
                SchemaIdsJson = n.SchemaIdsJson,
                Version = n.Version,
                FirstSeenAt = n.FirstSeenAt,
                LastUpdatedAt = n.LastUpdatedAt
            })
            .ToListAsync();
    }

    public async Task<UnderstandingNode?> GetNodeWithEdgesAsync(int nodeId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        
        // Use AsNoTracking + projection to avoid loading heavy embedding vectors
        // for the node itself and all connected nodes. The .Include().ThenInclude()
        // chain was causing a cartesian explosion loading SemanticEmbedding (1536 floats),
        // GraphEmbedding (128 floats), etc. for every connected entity.
        var node = await db.UnderstandingNodes
            .AsNoTracking()
            .Where(n => n.Id == nodeId)
            .Select(n => new UnderstandingNode
            {
                Id = n.Id,
                CanonicalText = n.CanonicalText,
                NormalizedKey = n.NormalizedKey,
                Status = n.Status,
                Confidence = n.Confidence,
                EvidenceCount = n.EvidenceCount,
                DegreeCentrality = n.DegreeCentrality,
                BetweennessCentrality = n.BetweennessCentrality,
                EigenvectorCentrality = n.EigenvectorCentrality,
                PageRank = n.PageRank,
                ClusteringCoefficient = n.ClusteringCoefficient,
                ControversyScore = n.ControversyScore,
                DialecticalTemperature = n.DialecticalTemperature,
                SchemaEntropy = n.SchemaEntropy,
                ArgumentIdsJson = n.ArgumentIdsJson,
                UserIdsJson = n.UserIdsJson,
                SchemaIdsJson = n.SchemaIdsJson,
                Version = n.Version,
                FirstSeenAt = n.FirstSeenAt,
                LastUpdatedAt = n.LastUpdatedAt,
                // Only load edge metadata — skip heavy SourceNode/TargetNode embeddings
                OutboundEdges = n.OutboundEdges.Select(e => new UnderstandingEdge
                {
                    Id = e.Id,
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    Relationship = e.Relationship,
                    Weight = e.Weight,
                    BaseWeight = e.BaseWeight,
                    ProvenanceJson = e.ProvenanceJson,
                    ReinforcementCount = e.ReinforcementCount,
                    CreatedAt = e.CreatedAt,
                    LastReinforcedAt = e.LastReinforcedAt,
                    // Only load the label for display — skip embeddings
                    TargetNode = new UnderstandingNode
                    {
                        Id = e.TargetNode.Id,
                        CanonicalText = e.TargetNode.CanonicalText,
                        Status = e.TargetNode.Status,
                        Confidence = e.TargetNode.Confidence
                    }
                }).ToList(),
                InboundEdges = n.InboundEdges.Select(e => new UnderstandingEdge
                {
                    Id = e.Id,
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    Relationship = e.Relationship,
                    Weight = e.Weight,
                    BaseWeight = e.BaseWeight,
                    ProvenanceJson = e.ProvenanceJson,
                    ReinforcementCount = e.ReinforcementCount,
                    CreatedAt = e.CreatedAt,
                    LastReinforcedAt = e.LastReinforcedAt,
                    SourceNode = new UnderstandingNode
                    {
                        Id = e.SourceNode.Id,
                        CanonicalText = e.SourceNode.CanonicalText,
                        Status = e.SourceNode.Status,
                        Confidence = e.SourceNode.Confidence
                    }
                }).ToList(),
                SchemaMemberships = n.SchemaMemberships.Select(m => new SchemaMembership
                {
                    NodeId = m.NodeId,
                    SchemaId = m.SchemaId,
                    Weight = m.Weight,
                    Schema = new ConceptualSchema
                    {
                        Id = m.Schema.Id,
                        Label = m.Schema.Label,
                        Description = m.Schema.Description,
                        DiscoveryMethod = m.Schema.DiscoveryMethod,
                        Coherence = m.Schema.Coherence,
                        Stability = m.Schema.Stability
                    }
                }).ToList()
            })
            .FirstOrDefaultAsync();

        return node;
    }

    public async Task<List<UnderstandingNode>> SearchNodesAsync(string query)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        if (string.IsNullOrWhiteSpace(query)) return await GetAllNodesAsync();
        
        // Use EF.Functions.ILike for database-side case-insensitive search
        // Fall back to Contains for each term if ILike is not available
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        IQueryable<UnderstandingNode> q = db.UnderstandingNodes.AsNoTracking();
        foreach (var term in terms)
        {
            var t = term; // capture for closure
            q = q.Where(n => EF.Functions.ILike(n.CanonicalText, $"%{t}%"));
        }
        
        return await q
            .OrderByDescending(n => n.Confidence)
            .Select(n => new UnderstandingNode
            {
                Id = n.Id,
                CanonicalText = n.CanonicalText,
                NormalizedKey = n.NormalizedKey,
                Status = n.Status,
                Confidence = n.Confidence,
                EvidenceCount = n.EvidenceCount,
                DegreeCentrality = n.DegreeCentrality,
                BetweennessCentrality = n.BetweennessCentrality,
                EigenvectorCentrality = n.EigenvectorCentrality,
                PageRank = n.PageRank,
                ClusteringCoefficient = n.ClusteringCoefficient,
                ControversyScore = n.ControversyScore,
                DialecticalTemperature = n.DialecticalTemperature,
                SchemaEntropy = n.SchemaEntropy,
                ArgumentIdsJson = n.ArgumentIdsJson,
                UserIdsJson = n.UserIdsJson,
                SchemaIdsJson = n.SchemaIdsJson,
                Version = n.Version,
                FirstSeenAt = n.FirstSeenAt,
                LastUpdatedAt = n.LastUpdatedAt
            })
            .ToListAsync();
    }

    public async Task<GraphStatistics> GetStatisticsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        // Use database-side aggregation instead of loading all nodes into memory
        var stats = await db.UnderstandingNodes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalNodes = g.Count(),
                SettledCount = g.Count(n => n.Status == PropositionStatus.Settled),
                ContestedCount = g.Count(n => n.Status == PropositionStatus.Contested),
                UnknownCount = g.Count(n => n.Status == PropositionStatus.Unknown),
                UnevaluatedCount = g.Count(n => n.Status == PropositionStatus.Unevaluated),
                AverageConfidence = g.Average(n => n.Confidence),
                TotalEvidenceItems = g.Sum(n => n.EvidenceCount)
            })
            .FirstOrDefaultAsync();

        return new GraphStatistics
        {
            TotalNodes = stats?.TotalNodes ?? 0,
            SettledCount = stats?.SettledCount ?? 0,
            ContestedCount = stats?.ContestedCount ?? 0,
            UnknownCount = stats?.UnknownCount ?? 0,
            UnevaluatedCount = stats?.UnevaluatedCount ?? 0,
            AverageConfidence = stats?.AverageConfidence ?? 0.5,
            TotalEvidenceItems = stats?.TotalEvidenceItems ?? 0
        };
    }

    // ── Private helpers (see UnderstandingGraphService.Helpers.cs) ─────────
}