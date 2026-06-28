using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Builds and maintains the sparse 3rd-order tensor T ∈ ℝ^(A × P × D)
/// where A = Arguments, P = Propositions (UnderstandingNodes), D = Dimensions.
///
/// T[a, p, d] = +1 if argument a asserts proposition p along dimension d,
///               -1 if argument a negates proposition p along dimension d,
///                0 otherwise (or ±confidence_score if adjudicated).
///
/// Supports incremental updates and exports to dense slices for decomposition.
/// </summary>
public class TensorConstructionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<TensorConstructionService> _logger;

    // Cached dimension names — the axes of the belief space
    private static readonly string[] DimensionNames = new[]
    {
        "political_equality", "economic_freedom", "social_authority", "individual_autonomy",
        "empirical_rationality", "moral_absolutism", "cultural_tradition", "progressive_change",
        "collective_welfare", "personal_responsibility", "fairness_proportionality",
        "care_harm", "loyalty_betrayal", "authority_subversion", "sanctity_degradation",
        "liberty_oppression", "universalism", "benevolence", "achievement", "security"
    };

    public TensorConstructionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<TensorConstructionService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Builds the full sparse tensor from current database state.
    /// Returns the tensor as a list of non-zero entries for downstream processing.
    /// </summary>
    public async Task<SparseTensor> BuildTensorAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var arguments = await db.Arguments.ToListAsync();
        var socialArgs = await db.SocialArguments.ToListAsync();
        var nodes = await db.UnderstandingNodes.ToListAsync();
        var edges = await db.UnderstandingEdges.ToListAsync();

        int aCount = arguments.Count + socialArgs.Count;
        int pCount = nodes.Count;
        int dCount = DimensionNames.Length;

        _logger.LogInformation("Building tensor: {A} arguments, {P} propositions, {D} dimensions.",
            aCount, pCount, dCount);

        var entries = new List<TensorEntry>();
        var argIndex = new Dictionary<string, int>();

        // Index analytical arguments
        int idx = 0;
        foreach (var arg in arguments)
        {
            argIndex[$"analytical:{arg.Id}"] = idx;
            // Map argument to its referenced propositions via edges
            var propIds = edges
                .Where(e => e.ProvenanceJson.Contains($"\"{arg.Id}\""))
                .Select(e => e.SourceNodeId)
                .Distinct()
                .ToList();

            foreach (var pid in propIds)
            {
                int pIdx = nodes.FindIndex(n => n.Id == pid);
                if (pIdx < 0) continue;

                // For each dimension, determine assertion/negation based on edge type
                for (int d = 0; d < dCount; d++)
                {
                    double val = EstimateTensorValue(edges, arg.Id, pid, d, nodes);
                    if (Math.Abs(val) > 0.01)
                        entries.Add(new TensorEntry(idx, pIdx, d, val));
                }
            }
            idx++;
        }

        // Index social arguments
        foreach (var sa in socialArgs)
        {
            argIndex[$"social:{sa.Id}"] = idx;
            var propIds = edges
                .Where(e => e.ProvenanceJson.Contains($"\"{sa.Id}\""))
                .Select(e => e.SourceNodeId)
                .Distinct()
                .ToList();

            foreach (var pid in propIds)
            {
                int pIdx = nodes.FindIndex(n => n.Id == pid);
                if (pIdx < 0) continue;

                for (int d = 0; d < dCount; d++)
                {
                    double val = EstimateTensorValue(edges, 0, pid, d, nodes);
                    if (Math.Abs(val) > 0.01)
                        entries.Add(new TensorEntry(idx, pIdx, d, val));
                }
            }
            idx++;
        }

        _logger.LogInformation("Tensor built: {Entries} non-zero entries ({Sparsity:P2} sparsity).",
            entries.Count, 1.0 - (double)entries.Count / (aCount * pCount * dCount));

        return new SparseTensor
        {
            ArgumentCount = aCount,
            PropositionCount = pCount,
            DimensionCount = dCount,
            DimensionNames = DimensionNames,
            Entries = entries,
            BuiltAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Estimates the tensor value T[a, p, d] based on edge relationships
    /// and node confidence scores.
    /// </summary>
    private static double EstimateTensorValue(
        List<UnderstandingEdge> edges, int argumentId, int propositionId,
        int dimensionIndex, List<UnderstandingNode> nodes)
    {
        var node = nodes.FirstOrDefault(n => n.Id == propositionId);
        if (node == null) return 0;

        // Find edges involving this proposition
        var relevantEdges = edges.Where(e =>
            e.SourceNodeId == propositionId || e.TargetNodeId == propositionId).ToList();

        if (relevantEdges.Count == 0) return 0;

        // Base value from confidence
        double baseVal = node.Confidence;

        // Modulate by edge relationships
        double supportWeight = relevantEdges
            .Where(e => e.Relationship is "supports" or "entails" or "refines")
            .Sum(e => e.Weight);
        double contradictWeight = relevantEdges
            .Where(e => e.Relationship is "contradicts" or "rebuts")
            .Sum(e => e.Weight);

        // If more contradiction than support, the proposition is negated along this dimension
        double net = baseVal * (1 + supportWeight - contradictWeight);

        // Clamp to [-1, 1]
        return Math.Max(-1.0, Math.Min(1.0, net));
    }

    /// <summary>
    /// Exports the tensor as a dense matrix slice for a specific dimension.
    /// Returns an Arguments × Propositions matrix.
    /// </summary>
    public Matrix<double> ExportDimensionSlice(SparseTensor tensor, int dimensionIndex)
    {
        var slice = Matrix<double>.Build.Dense(tensor.ArgumentCount, tensor.PropositionCount, 0);
        foreach (var entry in tensor.Entries.Where(e => e.Dimension == dimensionIndex))
            slice[entry.Argument, entry.Proposition] = entry.Value;
        return slice;
    }

    /// <summary>
    /// Exports the mode-1 unfolding (Arguments × (Propositions * Dimensions)).
    /// Used for SVD-based initialization of CP decomposition.
    /// </summary>
    public Matrix<double> ExportMode1Unfolding(SparseTensor tensor)
    {
        int rows = tensor.ArgumentCount;
        int cols = tensor.PropositionCount * tensor.DimensionCount;
        var matrix = Matrix<double>.Build.Dense(rows, cols, 0);
        foreach (var entry in tensor.Entries)
            matrix[entry.Argument, entry.Proposition * tensor.DimensionCount + entry.Dimension] = entry.Value;
        return matrix;
    }

    /// <summary>
    /// Incrementally updates the tensor when new arguments or propositions are added.
    /// </summary>
    public async Task<SparseTensor> UpdateTensorAsync(SparseTensor existing, DateTime since)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var newArgs = await db.Arguments.Where(a => a.CreatedAt > since).ToListAsync();
        var newSocialArgs = await db.SocialArguments.Where(sa => sa.CreatedAt > since).ToListAsync();
        var newNodes = await db.UnderstandingNodes.Where(n => n.FirstSeenAt > since).ToListAsync();
        var newEdges = await db.UnderstandingEdges.Where(e => e.CreatedAt > since).ToListAsync();

        if (newArgs.Count == 0 && newSocialArgs.Count == 0 && newNodes.Count == 0 && newEdges.Count == 0)
        {
            _logger.LogInformation("No new data since {Since}; tensor unchanged.", since);
            return existing;
        }

        _logger.LogInformation("Incremental update: {A} args, {P} props, {E} edges.",
            newArgs.Count + newSocialArgs.Count, newNodes.Count, newEdges.Count);

        // For simplicity, rebuild the full tensor incrementally
        // In production, this would append to the existing sparse structure
        return await BuildTensorAsync();
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

/// <summary>A single non-zero entry in the sparse tensor.</summary>
public record TensorEntry(int Argument, int Proposition, int Dimension, double Value);

/// <summary>Represents the sparse 3rd-order tensor T ∈ ℝ^(A × P × D).</summary>
public class SparseTensor
{
    public int ArgumentCount { get; init; }
    public int PropositionCount { get; init; }
    public int DimensionCount { get; init; }
    public string[] DimensionNames { get; init; } = Array.Empty<string>();
    public List<TensorEntry> Entries { get; init; } = new();
    public DateTime BuiltAt { get; init; }

    public double Sparsity =>
        1.0 - (double)Entries.Count / (ArgumentCount * PropositionCount * DimensionCount);
}