using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Topological Data Analysis (TDA) — applies persistent homology to the
/// Understanding Node embedding point cloud to discover topological features:
///
/// H₀ (Connected Components): Individual propositions merging into schemas
/// H₁ (Loops): Circular argument patterns / self-referential reasoning
/// H₂ (Voids): Conceptual blindspots — regions with no propositions
///
/// Uses a C# implementation of the Vietoris-Rips complex with persistent
/// homology via the union-find algorithm (for H₀) and a simplified approach
/// for higher-dimensional features.
///
/// For production-scale data, this would delegate to a Python backend
/// (ripser.py or gudhi) via a process bridge.
/// </summary>
public class TdaService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<TdaService> _logger;

    private const int MaxDimensions = 3; // H₀, H₁, H₂
    private const int MaxSimplices = 50000;

    public TdaService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<TdaService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ── Persistent Homology ───────────────────────────────────────────────

    /// <summary>
    /// Computes persistent homology on the UnderstandingNode embedding space.
    /// Returns persistence diagrams for H₀, H₁, and H₂.
    /// </summary>
    public async Task<TdaResult> ComputePersistenceAsync(int maxScale = 10, int scaleSteps = 50)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var nodes = await db.UnderstandingNodes
            .Where(n => n.SemanticEmbedding != null)
            .ToListAsync();

        if (nodes.Count < 10)
        {
            _logger.LogWarning("Too few embedded nodes ({Count}) for TDA.", nodes.Count);
            return new TdaResult();
        }

        _logger.LogInformation("Computing persistent homology on {Count} nodes.", nodes.Count);

        // Build distance matrix from semantic embeddings
        int n = nodes.Count;
        var distances = Matrix<double>.Build.Dense(n, n, 0);
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double d = EuclideanDistance(nodes[i].SemanticEmbedding!, nodes[j].SemanticEmbedding!);
                distances[i, j] = d;
                distances[j, i] = d;
            }
        }

        // Compute persistence for each homology dimension
        var diagrams = new List<PersistenceDiagram>();

        // H₀: Connected components — use union-find
        var h0Diagram = ComputeH0Persistence(n, distances, maxScale, scaleSteps);
        diagrams.Add(h0Diagram);

        // H₁: Loops — use simplified alpha approximation
        var h1Diagram = ComputeH1Persistence(n, distances, maxScale, scaleSteps);
        diagrams.Add(h1Diagram);

        // H₂: Voids — use simplified approximation
        var h2Diagram = ComputeH2Persistence(n, distances, maxScale, scaleSteps);
        diagrams.Add(h2Diagram);

        // Map topological features back to graph nodes
        var features = MapFeaturesToNodes(diagrams, nodes, distances);

        _logger.LogInformation("TDA complete: {H0} H₀ features, {H1} H₁ features, {H2} H₂ features.",
            h0Diagram.Features.Count, h1Diagram.Features.Count, h2Diagram.Features.Count);

        return new TdaResult
        {
            Diagrams = diagrams,
            Features = features,
            NodeCount = n,
            MaxScale = maxScale
        };
    }

    // ── H₀: Connected Components ──────────────────────────────────────────

    private static PersistenceDiagram ComputeH0Persistence(
        int n, Matrix<double> distances, int maxScale, int scaleSteps)
    {
        var features = new List<PersistenceFeature>();
        double step = maxScale / (double)scaleSteps;

        // Union-Find for tracking components across scales
        var parent = new int[n];
        var birth = new double[n];

        for (int i = 0; i < n; i++) { parent[i] = i; birth[i] = 0; }

        // Sort all edges by distance
        var edges = new List<(int i, int j, double d)>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                edges.Add((i, j, distances[i, j]));
        edges.Sort((a, b) => a.d.CompareTo(b.d));

        int edgeIdx = 0;
        int components = n;

        for (int s = 0; s <= scaleSteps; s++)
        {
            double scale = s * step;
            while (edgeIdx < edges.Count && edges[edgeIdx].d <= scale)
            {
                var (i, j, d) = edges[edgeIdx];
                int ri = Find(parent, i);
                int rj = Find(parent, j);
                if (ri != rj)
                {
                    // Merge: one component dies at this scale
                    parent[ri] = rj;
                    features.Add(new PersistenceFeature
                    {
                        Dimension = 0,
                        Birth = birth[ri],
                        Death = d,
                        NodeIndices = new List<int> { i, j },
                        Persistence = d - birth[ri]
                    });
                    components--;
                }
                edgeIdx++;
            }
        }

        // Remaining components are infinite (never die)
        var seen = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            int r = Find(parent, i);
            if (!seen.Contains(r))
            {
                seen.Add(r);
                features.Add(new PersistenceFeature
                {
                    Dimension = 0,
                    Birth = birth[r],
                    Death = maxScale,
                    NodeIndices = new List<int> { i },
                    Persistence = maxScale - birth[r]
                });
            }
        }

        return new PersistenceDiagram
        {
            Dimension = 0,
            Features = features.OrderByDescending(f => f.Persistence).ToList()
        };
    }

    // ── H₁: Loops (simplified) ────────────────────────────────────────────

    private static PersistenceDiagram ComputeH1Persistence(
        int n, Matrix<double> distances, int maxScale, int scaleSteps)
    {
        var features = new List<PersistenceFeature>();
        double step = maxScale / (double)scaleSteps;

        // Simplified loop detection: look for minimal cycles in the
        // neighborhood graph at each scale using a spanning tree approach.
        // A loop appears when adding an edge creates a cycle in the graph.

        var parent = new int[n];
        var rank = new int[n];
        for (int i = 0; i < n; i++) { parent[i] = i; rank[i] = 0; }

        var edges = new List<(int i, int j, double d)>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                edges.Add((i, j, distances[i, j]));
        edges.Sort((a, b) => a.d.CompareTo(b.d));

        int edgeIdx = 0;
        var loopCandidates = new List<(int i, int j, double d)>();

        // Build MST and collect edges that would create cycles
        while (edgeIdx < edges.Count)
        {
            var (i, j, d) = edges[edgeIdx];
            int ri = Find(parent, i);
            int rj = Find(parent, j);
            if (ri != rj)
            {
                // Union by rank
                if (rank[ri] < rank[rj]) parent[ri] = rj;
                else if (rank[ri] > rank[rj]) parent[rj] = ri;
                else { parent[rj] = ri; rank[ri]++; }
            }
            else
            {
                // This edge creates a cycle — potential H₁ feature
                loopCandidates.Add((i, j, d));
            }
            edgeIdx++;
        }

        // For each loop candidate, estimate birth and death
        foreach (var (i, j, d) in loopCandidates)
        {
            // Birth is the distance at which the loop forms
            // Death is the distance at which the loop is filled in
            // (approximated by the max edge in the cycle)
            double birth = d;
            double death = EstimateLoopDeath(i, j, distances, n);

            if (death - birth > step * 2) // Only keep persistent loops
            {
                features.Add(new PersistenceFeature
                {
                    Dimension = 1,
                    Birth = birth,
                    Death = death,
                    NodeIndices = new List<int> { i, j },
                    Persistence = death - birth
                });
            }
        }

        return new PersistenceDiagram
        {
            Dimension = 1,
            Features = features.OrderByDescending(f => f.Persistence).Take(20).ToList()
        };
    }

    // ── H₂: Voids (simplified) ────────────────────────────────────────────

    private static PersistenceDiagram ComputeH2Persistence(
        int n, Matrix<double> distances, int maxScale, int scaleSteps)
    {
        var features = new List<PersistenceFeature>();
        double step = maxScale / (double)scaleSteps;

        // Simplified void detection: look for regions of the embedding space
        // that are sparsely populated relative to the average density.
        // A "void" is a region with significantly lower point density than
        // its surroundings, persisting across multiple scales.

        // Compute local density for each point
        var densities = new double[n];
        for (int i = 0; i < n; i++)
        {
            var neighbors = new List<double>();
            for (int j = 0; j < n; j++)
                if (i != j) neighbors.Add(distances[i, j]);
            neighbors.Sort();
            // Density = inverse of average distance to k nearest neighbors
            int k = Math.Min(10, neighbors.Count);
            densities[i] = 1.0 / (1.0 + neighbors.Take(k).Average());
        }

        double avgDensity = densities.Average();
        double stdDensity = Math.Sqrt(densities.Select(d => (d - avgDensity) * (d - avgDensity)).Average());

        // Find low-density regions (potential voids)
        var lowDensityNodes = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (densities[i] < avgDensity - stdDensity)
                lowDensityNodes.Add(i);
        }

        // Cluster low-density nodes into void regions
        if (lowDensityNodes.Count >= 3)
        {
            var voidClusters = ClusterLowDensityNodes(lowDensityNodes, distances);

            foreach (var cluster in voidClusters)
            {
                if (cluster.Count < 3) continue;

                // Birth = min distance within cluster
                double birth = double.MaxValue;
                double death = 0;
                for (int i = 0; i < cluster.Count; i++)
                {
                    for (int j = i + 1; j < cluster.Count; j++)
                    {
                        double d = distances[cluster[i], cluster[j]];
                        if (d < birth) birth = d;
                        if (d > death) death = d;
                    }
                }

                features.Add(new PersistenceFeature
                {
                    Dimension = 2,
                    Birth = birth,
                    Death = death,
                    NodeIndices = new List<int>(cluster),
                    Persistence = death - birth
                });
            }
        }

        return new PersistenceDiagram
        {
            Dimension = 2,
            Features = features.OrderByDescending(f => f.Persistence).Take(10).ToList()
        };
    }

    // ── Feature Mapping ───────────────────────────────────────────────────

    private List<TopologicalFeature> MapFeaturesToNodes(
        List<PersistenceDiagram> diagrams,
        List<UnderstandingNode> nodes,
        Matrix<double> distances)
    {
        var features = new List<TopologicalFeature>();

        foreach (var diagram in diagrams)
        {
            foreach (var pf in diagram.Features.Where(f => f.Persistence > 0.5))
            {
                var featureNodes = pf.NodeIndices.Select(i => nodes[i].Id).ToList();

                string type = pf.Dimension switch
                {
                    0 => "connected_component",
                    1 => "loop",
                    2 => "void",
                    _ => "unknown"
                };

                features.Add(new TopologicalFeature
                {
                    Type = type,
                    Dimension = pf.Dimension,
                    Birth = pf.Birth,
                    Death = pf.Death,
                    Persistence = pf.Persistence,
                    NodeIds = featureNodes,
                    Description = pf.Dimension switch
                    {
                        0 => $"Component of {featureNodes.Count} propositions merging at scale {pf.Death:F2}",
                        1 => $"Circular reasoning pattern persisting from {pf.Birth:F2} to {pf.Death:F2}",
                        2 => $"Conceptual void — {featureNodes.Count} propositions at boundary, persisting {pf.Persistence:F2}",
                        _ => $"Topological feature at dimension {pf.Dimension}"
                    }
                });
            }
        }

        return features;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]]; // Path compression
            x = parent[x];
        }
        return x;
    }

    private static double EuclideanDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length) return double.MaxValue;
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double d = a[i] - b[i];
            sum += d * d;
        }
        return Math.Sqrt(sum);
    }

    private static double EstimateLoopDeath(int i, int j, Matrix<double> distances, int n)
    {
        // Estimate the scale at which this loop is filled in
        // by finding the maximum edge in the shortest path between i and j
        // that doesn't use the direct edge
        var visited = new bool[n];
        var maxEdge = new double[n];
        var queue = new Queue<int>();
        queue.Enqueue(i);
        visited[i] = true;

        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            if (v == j) return maxEdge[v];

            for (int u = 0; u < n; u++)
            {
                if (u == v || (v == i && u == j) || (v == j && u == i)) continue;
                if (!visited[u] && distances[v, u] < double.MaxValue)
                {
                    visited[u] = true;
                    maxEdge[u] = Math.Max(maxEdge[v], distances[v, u]);
                    queue.Enqueue(u);
                }
            }
        }

        return distances[i, j] * 2; // Fallback
    }

    private static List<List<int>> ClusterLowDensityNodes(
        List<int> lowDensityNodes, Matrix<double> distances)
    {
        var clusters = new List<List<int>>();
        var assigned = new HashSet<int>();

        foreach (var start in lowDensityNodes)
        {
            if (assigned.Contains(start)) continue;

            var cluster = new List<int> { start };
            assigned.Add(start);
            var queue = new Queue<int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                foreach (var u in lowDensityNodes)
                {
                    if (!assigned.Contains(u) && distances[v, u] < 1.0)
                    {
                        assigned.Add(u);
                        cluster.Add(u);
                        queue.Enqueue(u);
                    }
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

/// <summary>A single persistent homology feature.</summary>
public class PersistenceFeature
{
    public int Dimension { get; set; } // 0 = component, 1 = loop, 2 = void
    public double Birth { get; set; }
    public double Death { get; set; }
    public double Persistence { get; set; }
    public List<int> NodeIndices { get; set; } = new();
}

/// <summary>Persistence diagram for one homology dimension.</summary>
public class PersistenceDiagram
{
    public int Dimension { get; set; }
    public List<PersistenceFeature> Features { get; set; } = new();
}

/// <summary>A topological feature mapped back to graph nodes.</summary>
public class TopologicalFeature
{
    public string Type { get; set; } = string.Empty; // connected_component, loop, void
    public int Dimension { get; set; }
    public double Birth { get; set; }
    public double Death { get; set; }
    public double Persistence { get; set; }
    public List<int> NodeIds { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

/// <summary>Complete TDA result.</summary>
public class TdaResult
{
    public List<PersistenceDiagram> Diagrams { get; set; } = new();
    public List<TopologicalFeature> Features { get; set; } = new();
    public int NodeCount { get; set; }
    public int MaxScale { get; set; }
    public bool HasSignificantFeatures => Features.Any(f => f.Persistence > 0.5);
}