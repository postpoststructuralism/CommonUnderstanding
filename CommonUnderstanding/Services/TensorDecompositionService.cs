using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Performs CP (CANDECOMP/PARAFAC) decomposition on the sparse 3rd-order tensor
/// T ≈ Σᵣ λᵣ · aᵣ ∘ pᵣ ∘ dᵣ using Alternating Least Squares (ALS).
///
/// Each rank-r component (aᵣ, pᵣ, dᵣ) represents a latent conceptual schema:
///   aᵣ ∈ ℝ^A — which arguments load onto this schema
///   pᵣ ∈ ℝ^P — which propositions define this schema
///   dᵣ ∈ ℝ^D — which dimensions this schema operates on
///
/// The discovered factors are persisted as ConceptualSchema records with
/// their loading vectors stored as JSON.
/// </summary>
public class TensorDecompositionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly TensorConstructionService _tensorConstruction;
    private readonly ILogger<TensorDecompositionService> _logger;

    private const int MaxIterations = 100;
    private const double ConvergenceThreshold = 1e-6;

    public TensorDecompositionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        TensorConstructionService tensorConstruction,
        ILogger<TensorDecompositionService> logger)
    {
        _contextFactory = contextFactory;
        _tensorConstruction = tensorConstruction;
        _logger = logger;
    }

    // ── CP Decomposition via ALS ──────────────────────────────────────────

    /// <summary>
    /// Runs CP decomposition with the given rank R.
    /// Returns factor matrices: A (A×R), P (P×R), D (D×R), and lambda vector (R).
    /// </summary>
    public async Task<CpDecompositionResult> DecomposeAsync(int rank = 10)
    {
        _logger.LogInformation("Starting CP decomposition with rank {Rank}.", rank);

        var tensor = await _tensorConstruction.BuildTensorAsync();
        if (tensor.Entries.Count == 0)
        {
            _logger.LogWarning("Tensor is empty; cannot decompose.");
            return new CpDecompositionResult(rank, 0, 0, 0);
        }

        int A = tensor.ArgumentCount;
        int P = tensor.PropositionCount;
        int D = tensor.DimensionCount;

        // Initialize factor matrices with random values
        var rng = new Random(42);
        var A_factors = Matrix<double>.Build.Dense(A, rank, (i, j) => rng.NextDouble());
        var P_factors = Matrix<double>.Build.Dense(P, rank, (i, j) => rng.NextDouble());
        var D_factors = Matrix<double>.Build.Dense(D, rank, (i, j) => rng.NextDouble());

        // Normalize initial columns
        for (int r = 0; r < rank; r++)
        {
            NormalizeColumn(A_factors, r);
            NormalizeColumn(P_factors, r);
            NormalizeColumn(D_factors, r);
        }

        double prevError = double.MaxValue;

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // ALS update steps
            // 1. Update A: A = T_(1) * (P ⊙ D) * (P^T P * D^T D)^†
            var PtP = P_factors.Transpose() * P_factors;
            var DtD = D_factors.Transpose() * D_factors;
            var V = PointwiseMultiply(PtP, DtD);
            var V_inv = V.Inverse();

            // Build the Khatri-Rao product (P ⊙ D) and compute A
            var A_update = Matrix<double>.Build.Dense(A, rank, 0);
            foreach (var entry in tensor.Entries)
            {
                for (int r = 0; r < rank; r++)
                    A_update[entry.Argument, r] += entry.Value * P_factors[entry.Proposition, r] * D_factors[entry.Dimension, r];
            }
            A_factors = A_update * V_inv;

            // 2. Update P: P = T_(2) * (D ⊙ A) * (D^T D * A^T A)^†
            var DtD2 = D_factors.Transpose() * D_factors;
            var AtA = A_factors.Transpose() * A_factors;
            var V2 = PointwiseMultiply(DtD2, AtA);
            var V2_inv = V2.Inverse();

            var P_update = Matrix<double>.Build.Dense(P, rank, 0);
            foreach (var entry in tensor.Entries)
            {
                for (int r = 0; r < rank; r++)
                    P_update[entry.Proposition, r] += entry.Value * D_factors[entry.Dimension, r] * A_factors[entry.Argument, r];
            }
            P_factors = P_update * V2_inv;

            // 3. Update D: D = T_(3) * (A ⊙ P) * (A^T A * P^T P)^†
            var AtA2 = A_factors.Transpose() * A_factors;
            var PtP2 = P_factors.Transpose() * P_factors;
            var V3 = PointwiseMultiply(AtA2, PtP2);
            var V3_inv = V3.Inverse();

            var D_update = Matrix<double>.Build.Dense(D, rank, 0);
            foreach (var entry in tensor.Entries)
            {
                for (int r = 0; r < rank; r++)
                    D_update[entry.Dimension, r] += entry.Value * A_factors[entry.Argument, r] * P_factors[entry.Proposition, r];
            }
            D_factors = D_update * V3_inv;

            // Normalize and compute lambda weights
            var lambda = new double[rank];
            for (int r = 0; r < rank; r++)
            {
                double normA = A_factors.Column(r).L2Norm();
                double normP = P_factors.Column(r).L2Norm();
                double normD = D_factors.Column(r).L2Norm();
                lambda[r] = normA * normP * normD;
                if (lambda[r] > 1e-12)
                {
                    A_factors.SetColumn(r, A_factors.Column(r) / normA);
                    P_factors.SetColumn(r, P_factors.Column(r) / normP);
                    D_factors.SetColumn(r, D_factors.Column(r) / normD);
                }
            }

            // Compute reconstruction error
            double error = ComputeFitError(tensor, A_factors, P_factors, D_factors, lambda);
            double relChange = Math.Abs(prevError - error) / Math.Max(1e-12, prevError);

            _logger.LogDebug("ALS iteration {Iter}: fit error = {Error:F6}, rel change = {Change:F8}",
                iter + 1, error, relChange);

            if (relChange < ConvergenceThreshold)
            {
                _logger.LogInformation("ALS converged at iteration {Iter}.", iter + 1);
                break;
            }
            prevError = error;
        }

        // Persist discovered schemas
        var schemas = await PersistFactorsAsync(A_factors, P_factors, D_factors, tensor);

        _logger.LogInformation("CP decomposition complete: {Rank} factors, {Schemas} schemas persisted.", rank, schemas.Count);

        return new CpDecompositionResult(rank, A, P, D)
        {
            ArgumentFactors = A_factors,
            PropositionFactors = P_factors,
            DimensionFactors = D_factors,
            DiscoveredSchemas = schemas
        };
    }

    // ── Rank selection ────────────────────────────────────────────────────

    /// <summary>
    /// Determines the optimal rank R via cross-validation.
    /// Runs decomposition at multiple ranks and picks the one with best
    /// reconstruction error on held-out tensor entries.
    /// </summary>
    public async Task<int> EstimateOptimalRankAsync(int maxRank = 30, int cvFolds = 3)
    {
        var tensor = await _tensorConstruction.BuildTensorAsync();
        if (tensor.Entries.Count < 100) return Math.Min(5, maxRank);

        var rng = new Random(42);
        var shuffled = tensor.Entries.OrderBy(_ => rng.Next()).ToList();
        int foldSize = shuffled.Count / cvFolds;

        var errors = new List<(int Rank, double Error)>();

        for (int rank = 2; rank <= maxRank; rank += 2)
        {
            double cvError = 0;
            for (int fold = 0; fold < cvFolds; fold++)
            {
                var testEntries = shuffled.Skip(fold * foldSize).Take(foldSize).ToList();
                var trainEntries = shuffled.Except(testEntries).ToList();
                var trainTensor = new SparseTensor
                {
                    ArgumentCount = tensor.ArgumentCount,
                    PropositionCount = tensor.PropositionCount,
                    DimensionCount = tensor.DimensionCount,
                    DimensionNames = tensor.DimensionNames,
                    Entries = trainEntries
                };

                // Quick decomposition on training set
                var result = await QuickDecomposeAsync(trainTensor, rank);
                double error = ComputeFitError(testEntries, result);
                cvError += error;
            }
            errors.Add((rank, cvError / cvFolds));
            _logger.LogDebug("Rank {Rank}: CV error = {Error:F6}", rank, cvError / cvFolds);
        }

        // Pick rank with lowest CV error
        var best = errors.OrderBy(e => e.Error).First();
        _logger.LogInformation("Optimal rank estimated: {Rank} (error = {Error:F6}).", best.Rank, best.Error);
        return best.Rank;
    }

    // ── Persistence ───────────────────────────────────────────────────────

    private async Task<List<ConceptualSchema>> PersistFactorsAsync(
        Matrix<double> A_factors, Matrix<double> P_factors, Matrix<double> D_factors,
        SparseTensor tensor)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var schemas = new List<ConceptualSchema>();

        int rank = A_factors.ColumnCount;
        var nodes = await db.UnderstandingNodes.ToListAsync();

        for (int r = 0; r < rank; r++)
        {
            // Get top-loading propositions for this factor
            var propLoadings = new List<(int NodeId, double Weight)>();
            for (int p = 0; p < P_factors.RowCount; p++)
            {
                double w = Math.Abs(P_factors[p, r]);
                if (w > 0.1)
                    propLoadings.Add((nodes[p].Id, Math.Round(w, 4)));
            }
            propLoadings = propLoadings.OrderByDescending(x => x.Weight).Take(20).ToList();

            if (propLoadings.Count < 2) continue; // Skip trivial factors

            // Get top-loading dimensions
            var dimLoadings = new List<(string Name, double Weight)>();
            for (int d = 0; d < D_factors.RowCount; d++)
            {
                double w = Math.Abs(D_factors[d, r]);
                if (w > 0.1)
                    dimLoadings.Add((tensor.DimensionNames[d], Math.Round(w, 4)));
            }
            dimLoadings = dimLoadings.OrderByDescending(x => x.Weight).Take(10).ToList();

            // Get top-loading arguments
            var argLoadings = new List<(int ArgIndex, double Weight)>();
            for (int a = 0; a < A_factors.RowCount; a++)
            {
                double w = Math.Abs(A_factors[a, r]);
                if (w > 0.2)
                    argLoadings.Add((a, Math.Round(w, 4)));
            }
            argLoadings = argLoadings.OrderByDescending(x => x.Weight).Take(10).ToList();

            // Build description from top dimensions
            var dimDesc = string.Join(", ", dimLoadings.Select(d => $"{d.Name} ({d.Weight:F2})"));
            var label = dimLoadings.Count > 0
                ? $"Tensor Factor {r + 1}: {dimLoadings[0].Name}"
                : $"Tensor Factor {r + 1}";

            var schema = new ConceptualSchema
            {
                Label = label,
                Description = $"Latent factor {r + 1} from CP decomposition. " +
                    $"Top dimensions: {dimDesc}. {propLoadings.Count} propositions.",
                DiscoveryMethod = "tensor_decomposition",
                Coherence = 0.0, // Will be computed by SchemaDiscoveryService
                Stability = 0.0,
                FactorIndex = r,
                DimensionLoadingsJson = JsonSerializer.Serialize(
                    dimLoadings.ToDictionary(d => d.Name, d => d.Weight)),
                PropositionLoadingsJson = JsonSerializer.Serialize(
                    propLoadings.ToDictionary(p => p.NodeId.ToString(), p => p.Weight)),
                ArgumentLoadingsJson = JsonSerializer.Serialize(
                    argLoadings.ToDictionary(a => a.ArgIndex.ToString(), a => a.Weight)),
                DiscoveredAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            db.ConceptualSchemas.Add(schema);
            await db.SaveChangesAsync();

            // Create membership records
            foreach (var (nodeId, weight) in propLoadings)
            {
                db.SchemaMemberships.Add(new SchemaMembership
                {
                    NodeId = nodeId,
                    SchemaId = schema.Id,
                    Weight = weight
                });

                var node = nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node != null)
                {
                    var schemaIds = DeserializeIntList(node.SchemaIdsJson);
                    if (!schemaIds.Contains(schema.Id))
                    {
                        schemaIds.Add(schema.Id);
                        node.SchemaIdsJson = JsonSerializer.Serialize(schemaIds);
                    }
                }
            }

            schemas.Add(schema);
        }

        await db.SaveChangesAsync();
        return schemas;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static Matrix<double> PointwiseMultiply(Matrix<double> a, Matrix<double> b)
    {
        int rows = a.RowCount, cols = a.ColumnCount;
        var result = Matrix<double>.Build.Dense(rows, cols);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = a[i, j] * b[i, j];
        return result;
    }

    private static void NormalizeColumn(Matrix<double> m, int col)
    {
        double norm = m.Column(col).L2Norm();
        if (norm > 1e-12)
            m.SetColumn(col, m.Column(col) / norm);
    }

    private static double ComputeFitError(
        SparseTensor tensor, Matrix<double> A, Matrix<double> P,
        Matrix<double> D, double[] lambda)
    {
        double error = 0;
        int count = 0;
        int rank = lambda.Length;

        foreach (var entry in tensor.Entries)
        {
            double reconstructed = 0;
            for (int r = 0; r < rank; r++)
                reconstructed += lambda[r] * A[entry.Argument, r] * P[entry.Proposition, r] * D[entry.Dimension, r];

            double diff = entry.Value - reconstructed;
            error += diff * diff;
            count++;
        }

        return count > 0 ? Math.Sqrt(error / count) : 0;
    }

    private static double ComputeFitError(
        List<TensorEntry> entries, CpDecompositionResult result)
    {
        double error = 0;
        int count = entries.Count;
        int rank = result.Rank;

        foreach (var entry in entries)
        {
            double reconstructed = 0;
            for (int r = 0; r < rank; r++)
                reconstructed += result.Lambda[r] *
                    result.ArgumentFactors[entry.Argument, r] *
                    result.PropositionFactors[entry.Proposition, r] *
                    result.DimensionFactors[entry.Dimension, r];

            double diff = entry.Value - reconstructed;
            error += diff * diff;
        }

        return count > 0 ? Math.Sqrt(error / count) : 0;
    }

    private async Task<CpDecompositionResult> QuickDecomposeAsync(SparseTensor tensor, int rank)
    {
        int A = tensor.ArgumentCount, P = tensor.PropositionCount, D = tensor.DimensionCount;
        var rng = new Random(42);
        var A_f = Matrix<double>.Build.Dense(A, rank, (i, j) => rng.NextDouble());
        var P_f = Matrix<double>.Build.Dense(P, rank, (i, j) => rng.NextDouble());
        var D_f = Matrix<double>.Build.Dense(D, rank, (i, j) => rng.NextDouble());

        for (int r = 0; r < rank; r++) { NormalizeColumn(A_f, r); NormalizeColumn(P_f, r); NormalizeColumn(D_f, r); }

        for (int iter = 0; iter < 20; iter++)
        {
            var PtP = P_f.Transpose() * P_f;
            var DtD = D_f.Transpose() * D_f;
            var V = PointwiseMultiply(PtP, DtD).Inverse();
            var A_up = Matrix<double>.Build.Dense(A, rank, 0);
            foreach (var e in tensor.Entries)
                for (int r = 0; r < rank; r++)
                    A_up[e.Argument, r] += e.Value * P_f[e.Proposition, r] * D_f[e.Dimension, r];
            A_f = A_up * V;

            var DtD2 = D_f.Transpose() * D_f;
            var AtA = A_f.Transpose() * A_f;
            var V2 = PointwiseMultiply(DtD2, AtA).Inverse();
            var P_up = Matrix<double>.Build.Dense(P, rank, 0);
            foreach (var e in tensor.Entries)
                for (int r = 0; r < rank; r++)
                    P_up[e.Proposition, r] += e.Value * D_f[e.Dimension, r] * A_f[e.Argument, r];
            P_f = P_up * V2;

            var AtA2 = A_f.Transpose() * A_f;
            var PtP2 = P_f.Transpose() * P_f;
            var V3 = PointwiseMultiply(AtA2, PtP2).Inverse();
            var D_up = Matrix<double>.Build.Dense(D, rank, 0);
            foreach (var e in tensor.Entries)
                for (int r = 0; r < rank; r++)
                    D_up[e.Dimension, r] += e.Value * A_f[e.Argument, r] * P_f[e.Proposition, r];
            D_f = D_up * V3;

            for (int r = 0; r < rank; r++) { NormalizeColumn(A_f, r); NormalizeColumn(P_f, r); NormalizeColumn(D_f, r); }
        }

        var lambda = new double[rank];
        for (int r = 0; r < rank; r++)
        {
            double nA = A_f.Column(r).L2Norm();
            double nP = P_f.Column(r).L2Norm();
            double nD = D_f.Column(r).L2Norm();
            lambda[r] = nA * nP * nD;
        }

        return new CpDecompositionResult(rank, A, P, D)
        {
            ArgumentFactors = A_f, PropositionFactors = P_f, DimensionFactors = D_f,
            Lambda = lambda
        };
    }

    private static List<int> DeserializeIntList(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>(); }
        catch { return new List<int>(); }
    }
}

/// <summary>
/// Result of CP tensor decomposition containing factor matrices and metadata.
/// </summary>
public class CpDecompositionResult
{
    public int Rank { get; }
    public int ArgumentCount { get; }
    public int PropositionCount { get; }
    public int DimensionCount { get; }
    public Matrix<double> ArgumentFactors { get; set; } = null!;
    public Matrix<double> PropositionFactors { get; set; } = null!;
    public Matrix<double> DimensionFactors { get; set; } = null!;
    public double[] Lambda { get; set; } = Array.Empty<double>();
    public List<ConceptualSchema> DiscoveredSchemas { get; set; } = new();

    public CpDecompositionResult(int rank, int argCount, int propCount, int dimCount)
    {
        Rank = rank;
        ArgumentCount = argCount;
        PropositionCount = propCount;
        DimensionCount = dimCount;
    }
}