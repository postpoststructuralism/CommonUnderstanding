using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Text.Json;

namespace CommonUnderstanding.Services.Social.Plugins;

/// <summary>
/// Computes the semantic convergence score between two Worldviews.
/// Uses embedding cosine similarity, argument Jaccard index, and Schwartz value alignment.
/// LLM is used ONLY for the optional narrative summary — not for score computation.
/// </summary>
public class WorldviewConvergencePlugin
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<WorldviewConvergencePlugin> _logger;

    // Schwartz value dimension index map (for radar chart alignment)
    public static readonly string[] SchwartzDimensions =
    {
        "SelfDirection", "Stimulation", "Hedonism", "Achievement", "Power",
        "Security", "Conformity", "Tradition", "Benevolence", "Universalism"
    };

    public WorldviewConvergencePlugin(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        SemanticKernelService kernelService,
        ILogger<WorldviewConvergencePlugin> logger)
    {
        _dbFactory = dbFactory;
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Computes a structured convergence result between two Worldviews.
    /// The score formula: 0.4 * semantic + 0.3 * argument_jaccard + 0.3 * schwartz_cosine
    /// </summary>
    [KernelFunction("ComputeConvergence")]
    [Description("Computes the semantic convergence score between two Worldviews.")]
    public async Task<ConvergenceResult> ComputeConvergenceAsync(
        [Description("ID of the first Worldview")] Guid worldviewAId,
        [Description("ID of the second Worldview")] Guid worldviewBId,
        bool includeNarrative = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var wvA = await LoadWorldviewAsync(db, worldviewAId, cancellationToken);
        var wvB = await LoadWorldviewAsync(db, worldviewBId, cancellationToken);

        if (wvA is null || wvB is null)
            throw new InvalidOperationException("One or both Worldviews not found.");

        // ── 1. Semantic cosine similarity ──────────────────────────────────────

        double semanticCosine = (wvA.Embedding is not null && wvB.Embedding is not null)
            ? ScoringAlgorithms.CosineSimilarity(wvA.Embedding, wvB.Embedding)
            : 0.0;

        // ── 2. Argument Jaccard index ──────────────────────────────────────────

        var argsA = new HashSet<Guid>(GetAllArgumentIds(wvA));
        var argsB = new HashSet<Guid>(GetAllArgumentIds(wvB));
        double argumentJaccard = ScoringAlgorithms.JaccardIndex(argsA, argsB);

        // ── 3. Schwartz value cosine similarity ────────────────────────────────

        double schwartzCosine = ScoringAlgorithms.CosineSimilarity(wvA.SchwartzVector, wvB.SchwartzVector);

        // ── 4. Weighted convergence score ──────────────────────────────────────

        double convergenceScore = ScoringAlgorithms.ConvergenceScore(semanticCosine, argumentJaccard, schwartzCosine);
        string classification = ScoringAlgorithms.ClassifyConvergence(convergenceScore);

        // ── 5. Schwartz breakdown ──────────────────────────────────────────────

        var schwartzBreakdown = BuildSchwartzBreakdown(wvA.SchwartzVector, wvB.SchwartzVector);

        // ── 6. Optional narrative (LLM) ────────────────────────────────────────

        string? narrative = null;
        if (includeNarrative)
        {
            narrative = await GenerateNarrativeAsync(
                wvA, wvB, argsA, argsB, convergenceScore, classification, cancellationToken);
        }

        return new ConvergenceResult(
            WorldviewAId: worldviewAId,
            WorldviewBId: worldviewBId,
            ConvergenceScore: convergenceScore,
            Classification: classification,
            SemanticSimilarity: semanticCosine,
            ArgumentJaccard: argumentJaccard,
            SchwartzAlignment: schwartzCosine,
            SharedArgumentIds: argsA.Intersect(argsB).ToList(),
            SchwartzBreakdown: schwartzBreakdown,
            Narrative: narrative);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Worldview?> LoadWorldviewAsync(
        ApplicationDbContext db,
        Guid worldviewId,
        CancellationToken ct)
    {
        return await db.Worldviews
            .AsNoTracking()
            .Include(w => w.WorldviewChains)
                .ThenInclude(wc => wc.ArgumentChain)
            .FirstOrDefaultAsync(w => w.Id == worldviewId, ct);
    }

    private static IEnumerable<Guid> GetAllArgumentIds(Worldview wv)
    {
        return wv.WorldviewChains
            .SelectMany(wc => wc.ArgumentChain?.ArgumentIds ?? Array.Empty<Guid>())
            .Distinct();
    }

    private static Dictionary<string, double> BuildSchwartzBreakdown(double[] vecA, double[] vecB)
    {
        var breakdown = new Dictionary<string, double>();
        for (int i = 0; i < SchwartzDimensions.Length; i++)
        {
            if (i >= vecA.Length || i >= vecB.Length) continue;
            // Alignment per dimension: 1 - abs(a - b) normalized to [0, 1]
            double maxVal = Math.Max(Math.Abs(vecA[i]), Math.Abs(vecB[i]));
            double alignment = maxVal == 0 ? 1.0 : 1.0 - Math.Abs(vecA[i] - vecB[i]) / maxVal;
            breakdown[SchwartzDimensions[i]] = Math.Round(Math.Clamp(alignment, 0.0, 1.0), 3);
        }
        return breakdown;
    }

    private async Task<string?> GenerateNarrativeAsync(
        Worldview wvA,
        Worldview wvB,
        HashSet<Guid> argsA,
        HashSet<Guid> argsB,
        double score,
        string classification,
        CancellationToken ct)
    {
        try
        {
            var kernel = _kernelService.GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var shared = argsA.Intersect(argsB).Count();
            var prompt = $"""
                Two worldviews have been compared. Their convergence score is {score:F2} ({classification}).
                Worldview A: "{wvA.Title}" with Schwartz values: {string.Join(", ", wvA.SchwartzValues.Take(3))}
                Worldview B: "{wvB.Title}" with Schwartz values: {string.Join(", ", wvB.SchwartzValues.Take(3))}
                They share {shared} arguments in common.
                
                Write exactly 2 sentences summarizing where these worldviews converge and diverge.
                Be specific, objective, and constructive. Do not mention scores.
                """;

            var history = new ChatHistory();
            history.AddSystemMessage("You are a neutral epistemic analyst comparing belief systems.");
            history.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
            return response.Content?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Convergence narrative generation failed.");
            return null;
        }
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

public record ConvergenceResult(
    Guid WorldviewAId,
    Guid WorldviewBId,
    double ConvergenceScore,
    string Classification,
    double SemanticSimilarity,
    double ArgumentJaccard,
    double SchwartzAlignment,
    List<Guid> SharedArgumentIds,
    Dictionary<string, double> SchwartzBreakdown,
    string? Narrative);
