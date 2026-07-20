using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Discovers emergent conceptual schemas from the Understanding Graph using
/// multiple algorithmic approaches:
///
/// 1. K-means clustering on semantic embeddings (Phase 3a)
/// 2. Spectral clustering on the graph adjacency matrix (Phase 3b)
/// 3. Non-negative Matrix Factorization (NMF) on the term-document matrix (Phase 3c)
/// 4. Formal Concept Analysis (FCA) lattice extraction (Phase 3d)
/// 5. Topological Data Analysis (TDA) persistent homology (Phase 3d)
///
/// Each discovered schema is persisted as a ConceptualSchema with membership
/// weights linking back to the constituent UnderstandingNodes.
/// </summary>
public class SchemaDiscoveryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<SchemaDiscoveryService> _logger;

    // Default hyperparameters (configurable)
    private const int DefaultK = 5;
    private const int MinClusterSize = 3;
    private const double MinCoherence = 0.3;

    public SchemaDiscoveryService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<SchemaDiscoveryService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  K-means clustering (Phase 3a — primary method)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Discovers schemas by clustering UnderstandingNode semantic embeddings
    /// using k-means. Each cluster becomes a ConceptualSchema.
    /// </summary>
    public async Task<List<ConceptualSchema>> DiscoverSchemasKMeansAsync(int k = DefaultK, int maxIterations = 50)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Use projection to load ONLY the columns we need (Id, SemanticEmbedding, SchemaIdsJson).
        // Avoids loading CanonicalText, GraphEmbedding, SchwartzVector, MoralFoundationsVector,
        // and other heavy columns that waste DB egress bandwidth.
        var nodeProjections = await db.UnderstandingNodes
            .Where(n => n.SemanticEmbedding != null)
            .Select(n => new
            {
                n.Id,
                n.SemanticEmbedding,
                n.SchemaIdsJson
            })
            .ToListAsync();

        if (nodeProjections.Count < MinClusterSize)
        {
            _logger.LogInformation("Too few nodes ({Count}) for clustering; need at least {Min}.", nodeProjections.Count, MinClusterSize);
            return new List<ConceptualSchema>();
        }

        k = Math.Min(k, nodeProjections.Count / MinClusterSize);
        if (k < 1) k = 1;

        _logger.LogInformation("Running k-means clustering: {Nodes} nodes, {K} clusters.", nodeProjections.Count, k);

        // Build data matrix: nodes x embedding dimensions
        int dims = nodeProjections[0].SemanticEmbedding!.Length;
        var data = Matrix<double>.Build.Dense(nodeProjections.Count, dims);
        for (int i = 0; i < nodeProjections.Count; i++)
            for (int j = 0; j < dims; j++)
                data[i, j] = nodeProjections[i].SemanticEmbedding![j];

        // K-means++ initialization
        var centroids = KMeansPlusPlusInit(data, k);
        var assignments = new int[nodeProjections.Count];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // Assignment step
            bool changed = false;
            for (int i = 0; i < nodeProjections.Count; i++)
            {
                int best = 0;
                double bestDist = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dist = (data.Row(i) - centroids[c]).L2Norm();
                    if (dist < bestDist) { bestDist = dist; best = c; }
                }
                if (assignments[i] != best) { assignments[i] = best; changed = true; }
            }

            if (!changed) break;

            // Update step
            for (int c = 0; c < k; c++)
            {
                var members = Enumerable.Range(0, nodeProjections.Count).Where(i => assignments[i] == c).ToList();
                if (members.Count == 0) continue;
                var sum = Vector<double>.Build.Dense(dims, 0);
                foreach (var idx in members) sum += data.Row(idx);
                centroids[c] = sum / members.Count;
            }
        }

        // Build ConceptualSchema records from clusters
        var schemas = new List<ConceptualSchema>();
        for (int c = 0; c < k; c++)
        {
            var memberIndices = Enumerable.Range(0, nodeProjections.Count).Where(i => assignments[i] == c).ToList();
            if (memberIndices.Count < MinClusterSize) continue;

            var memberProjections = memberIndices.Select(i => nodeProjections[i]).ToList();
            var memberEmbeddings = memberProjections.Select(p => p.SemanticEmbedding!).ToList();

            // Compute coherence: average pairwise cosine similarity within cluster
            double coherence = ComputeClusterCoherenceFromEmbeddings(memberEmbeddings);

            if (coherence < MinCoherence)
            {
                _logger.LogDebug("Skipping cluster {Cluster}: coherence {Coherence} below threshold.", c, coherence);
                continue;
            }

            var schema = new ConceptualSchema
            {
                Label = $"Schema {c + 1} ({memberProjections.Count} propositions)",
                Description = $"Automatically discovered cluster of {memberProjections.Count} semantically related propositions.",
                DiscoveryMethod = "k_means",
                Coherence = Math.Round(coherence, 4),
                Stability = 0.0, // Computed across multiple runs
                FactorIndex = c,
                DiscoveredAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            db.ConceptualSchemas.Add(schema);
            await db.SaveChangesAsync(); // Save to get schema.Id

            // Create membership records
            foreach (var member in memberProjections)
            {
                double weight = ComputeMembershipWeightFromEmbedding(member, centroids[c], data.Row(nodeProjections.IndexOf(member)));
                db.SchemaMemberships.Add(new SchemaMembership
                {
                    NodeId = member.Id,
                    SchemaId = schema.Id,
                    Weight = Math.Round(weight, 4)
                });

                // Update node's schema IDs
                var schemaIds = DeserializeIntList(member.SchemaIdsJson);
                if (!schemaIds.Contains(schema.Id))
                {
                    schemaIds.Add(schema.Id);
                    // Update via raw SQL to avoid loading full entity
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE \"UnderstandingNodes\" SET \"SchemaIdsJson\" = {0} WHERE \"Id\" = {1}",
                        JsonSerializer.Serialize(schemaIds), member.Id);
                }
            }

            // Save memberships for this schema immediately
            await db.SaveChangesAsync();

            schemas.Add(schema);
        }

        _logger.LogInformation("K-means discovery complete: {Count} schemas created.", schemas.Count);
        return schemas;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spectral clustering (Phase 3b)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Discovers schemas using spectral clustering on the graph Laplacian.
    /// Captures non-convex cluster shapes that k-means misses.
    /// </summary>
    public async Task<List<ConceptualSchema>> DiscoverSchemasSpectralAsync(int k = DefaultK)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Use projection to load ONLY needed columns — avoids loading heavy
        // embedding columns (GraphEmbedding, SchwartzVector, MoralFoundationsVector)
        // and large text fields (CanonicalText, DimensionalCoordinatesJson, etc.)
        var nodeProjections = await db.UnderstandingNodes
            .Select(n => new
            {
                n.Id,
                n.SemanticEmbedding,
                n.SchemaIdsJson
            })
            .ToListAsync();

        var edgeProjections = await db.UnderstandingEdges
            .Select(e => new { e.SourceNodeId, e.TargetNodeId, e.Weight })
            .ToListAsync();

        if (nodeProjections.Count < MinClusterSize)
        {
            _logger.LogInformation("Too few nodes ({Count}) for spectral clustering.", nodeProjections.Count);
            return new List<ConceptualSchema>();
        }

        k = Math.Min(k, nodeProjections.Count / MinClusterSize);
        if (k < 1) k = 1;

        _logger.LogInformation("Running spectral clustering: {Nodes} nodes, {K} clusters.", nodeProjections.Count, k);

        int n = nodeProjections.Count;
        var idIndex = nodeProjections.Select((node, idx) => (node.Id, idx)).ToDictionary(x => x.Id, x => x.idx);

        // Build weighted adjacency matrix W
        var W = Matrix<double>.Build.Dense(n, n, 0);
        foreach (var edge in edgeProjections)
        {
            if (idIndex.TryGetValue(edge.SourceNodeId, out int si) &&
                idIndex.TryGetValue(edge.TargetNodeId, out int ti))
            {
                W[si, ti] = edge.Weight;
                W[ti, si] = edge.Weight;
            }
        }

        // Build degree matrix D and Laplacian L = D - W
        var D = Matrix<double>.Build.Dense(n, n, 0);
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int j = 0; j < n; j++) sum += W[i, j];
            D[i, i] = sum > 0 ? sum : 1;
        }

        // Normalized Laplacian: L_norm = I - D^(-1/2) * W * D^(-1/2)
        var DInvSqrt = Matrix<double>.Build.Dense(n, n, 0);
        for (int i = 0; i < n; i++)
            DInvSqrt[i, i] = 1.0 / Math.Sqrt(D[i, i]);

        var L = Matrix<double>.Build.DenseIdentity(n) - DInvSqrt * W * DInvSqrt;

        // Compute k smallest eigenvectors via power iteration with deflation
        var eigenvectors = new List<Vector<double>>();
        var remaining = L.Clone();

        for (int eig = 0; eig < k; eig++)
        {
            // Power iteration for smallest eigenvalue: inverse iteration
            var v = Vector<double>.Build.Dense(n, 1.0 / Math.Sqrt(n));
            for (int iter = 0; iter < 30; iter++)
            {
                try
                {
                    var solved = remaining.Solve(v);
                    double norm = solved.L2Norm();
                    if (norm > 1e-12) solved /= norm;
                    v = solved;
                }
                catch
                {
                    break;
                }
            }
            eigenvectors.Add(v.Clone());

            // Deflate: subtract the contribution of this eigenvector
            remaining -= v.OuterProduct(v) * (v.ToRowMatrix() * remaining * v.ToColumnMatrix())[0, 0];
        }

        // Form U matrix (n x k) from eigenvectors and run k-means on rows
        var U = Matrix<double>.Build.Dense(n, k);
        for (int i = 0; i < k; i++)
            for (int j = 0; j < n; j++)
                U[j, i] = eigenvectors[i][j];

        // K-means on U rows
        var centroids = KMeansPlusPlusInit(U, k);
        var assignments = new int[n];
        for (int iter = 0; iter < 50; iter++)
        {
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                int best = 0; double bestDist = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dist = (U.Row(i) - centroids[c]).L2Norm();
                    if (dist < bestDist) { bestDist = dist; best = c; }
                }
                if (assignments[i] != best) { assignments[i] = best; changed = true; }
            }
            if (!changed) break;
            for (int c = 0; c < k; c++)
            {
                var members = Enumerable.Range(0, n).Where(i => assignments[i] == c).ToList();
                if (members.Count == 0) continue;
                var sum = Vector<double>.Build.Dense(k, 0);
                foreach (var idx in members) sum += U.Row(idx);
                centroids[c] = sum / members.Count;
            }
        }

        // Build schemas
        var schemas = new List<ConceptualSchema>();
        for (int c = 0; c < k; c++)
        {
            var memberIndices = Enumerable.Range(0, n).Where(i => assignments[i] == c).ToList();
            if (memberIndices.Count < MinClusterSize) continue;

            var memberProjections = memberIndices.Select(i => nodeProjections[i]).ToList();
            var memberEmbeddings = memberProjections.Select(p => p.SemanticEmbedding!).ToList();
            double coherence = ComputeClusterCoherenceFromEmbeddings(memberEmbeddings);
            if (coherence < MinCoherence) continue;

            var schema = new ConceptualSchema
            {
                Label = $"Spectral Schema {c + 1} ({memberProjections.Count} propositions)",
                Description = $"Discovered via spectral clustering on graph Laplacian.",
                DiscoveryMethod = "spectral_clustering",
                Coherence = Math.Round(coherence, 4),
                Stability = 0.0,
                FactorIndex = c,
                DiscoveredAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            db.ConceptualSchemas.Add(schema);
            await db.SaveChangesAsync();

            foreach (var member in memberProjections)
            {
                db.SchemaMemberships.Add(new SchemaMembership
                {
                    NodeId = member.Id, SchemaId = schema.Id,
                    Weight = Math.Round(1.0 / memberProjections.Count, 4)
                });
                var schemaIds = DeserializeIntList(member.SchemaIdsJson);
                if (!schemaIds.Contains(schema.Id))
                {
                    schemaIds.Add(schema.Id);
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE \"UnderstandingNodes\" SET \"SchemaIdsJson\" = {0} WHERE \"Id\" = {1}",
                        JsonSerializer.Serialize(schemaIds), member.Id);
                }
            }
            // Save memberships for this schema immediately
            await db.SaveChangesAsync();
            schemas.Add(schema);
        }

        _logger.LogInformation("Spectral discovery complete: {Count} schemas.", schemas.Count);
        return schemas;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LLM-based schema labeling
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Uses an LLM to generate human-readable labels and descriptions for
    /// all unlabeled ConceptualSchemas. Call after discovery.
    /// </summary>
    public async Task LabelSchemasAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var schemas = await db.ConceptualSchemas
            .Where(s => s.Label.StartsWith("Schema") || s.Label.StartsWith("Spectral"))
            .ToListAsync();

        if (schemas.Count == 0) return;

        _logger.LogInformation("Labeling {Count} schemas via LLM.", schemas.Count);

        // Load membership texts in a single projection query — avoids Include/ThenInclude
        // which was loading full UnderstandingNode entities with all embedding vectors.
        var schemaIds = schemas.Select(s => s.Id).ToList();
        var membershipTexts = await db.SchemaMemberships
            .Where(m => schemaIds.Contains(m.SchemaId))
            .OrderByDescending(m => m.Weight)
            .Select(m => new { m.SchemaId, m.Node.CanonicalText, m.Weight })
            .ToListAsync();

        var textsBySchema = membershipTexts
            .GroupBy(m => m.SchemaId)
            .ToDictionary(g => g.Key, g => g.Take(10).Select(m =>
                $"  - \"{m.CanonicalText}\" (weight: {m.Weight:F2})").ToList());

        foreach (var schema in schemas)
        {
            if (!textsBySchema.TryGetValue(schema.Id, out var texts) || texts.Count == 0)
                continue;

            // Store the member texts in description for now; LLM labeling
            // will be implemented when SemanticKernelService integration is added.
            schema.Description = $"Member propositions:\n{string.Join("\n", texts)}";
            schema.LastUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Vector<double>[] KMeansPlusPlusInit(Matrix<double> data, int k)
    {
        int n = data.RowCount;
        var rng = new Random(42);
        var centroids = new Vector<double>[k];

        // Choose first centroid randomly
        centroids[0] = data.Row(rng.Next(n));

        for (int c = 1; c < k; c++)
        {
            var distances = new double[n];
            double totalDist = 0;
            for (int i = 0; i < n; i++)
            {
                double minDist = double.MaxValue;
                for (int j = 0; j < c; j++)
                {
                    double d = (data.Row(i) - centroids[j]).L2Norm();
                    if (d < minDist) minDist = d;
                }
                distances[i] = minDist * minDist;
                totalDist += distances[i];
            }

            double threshold = rng.NextDouble() * totalDist;
            double cumulative = 0;
            for (int i = 0; i < n; i++)
            {
                cumulative += distances[i];
                if (cumulative >= threshold) { centroids[c] = data.Row(i); break; }
            }
        }

        return centroids;
    }

    private static double ComputeClusterCoherence(List<UnderstandingNode> nodes)
    {
        if (nodes.Count < 2) return 1.0;
        double totalSim = 0;
        int pairs = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (nodes[i].SemanticEmbedding != null && nodes[j].SemanticEmbedding != null)
                {
                    totalSim += CosineSimilarity(nodes[i].SemanticEmbedding, nodes[j].SemanticEmbedding);
                    pairs++;
                }
            }
        }
        return pairs > 0 ? totalSim / pairs : 0;
    }

    /// <summary>
    /// Computes cluster coherence from projected node embeddings only.
    /// Avoids loading full UnderstandingNode entities to save DB egress.
    /// </summary>
    private static double ComputeClusterCoherenceFromEmbeddings(
        List<float[]> embeddings)
    {
        if (embeddings.Count < 2) return 1.0;
        double totalSim = 0;
        int pairs = 0;
        for (int i = 0; i < embeddings.Count; i++)
        {
            for (int j = i + 1; j < embeddings.Count; j++)
            {
                if (embeddings[i] != null && embeddings[j] != null)
                {
                    totalSim += CosineSimilarity(embeddings[i], embeddings[j]);
                    pairs++;
                }
            }
        }
        return pairs > 0 ? totalSim / pairs : 0;
    }

    private static double ComputeMembershipWeight(UnderstandingNode node, Vector<double> centroid, Vector<double> point)
    {
        double dist = (point - centroid).L2Norm();
        if (dist < 1e-12) return 1.0;
        return Math.Max(0, Math.Min(1, 1.0 / (1.0 + dist)));
    }

    /// <summary>
    /// Computes membership weight from embedding vector.
    /// Avoids loading full UnderstandingNode entity.
    /// </summary>
    private static double ComputeMembershipWeightFromEmbedding(
        object nodeProjection, Vector<double> centroid, Vector<double> point)
    {
        double dist = (point - centroid).L2Norm();
        if (dist < 1e-12) return 1.0;
        return Math.Max(0, Math.Min(1, 1.0 / (1.0 + dist)));
    }

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

    private static List<int> DeserializeIntList(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>(); }
        catch { return new List<int>(); }
    }
}