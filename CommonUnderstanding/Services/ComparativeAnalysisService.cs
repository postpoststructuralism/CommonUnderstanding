using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Runs a head-to-head comparison between two fully-decomposed arguments,
/// identifying conflicting premises, shared ground, and net epistemic direction.
/// Uses a single consolidated LLM call for the full analysis.
/// </summary>
public class ComparativeAnalysisService
{
    private readonly ApplicationDbContext _db;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<ComparativeAnalysisService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public ComparativeAnalysisService(
        ApplicationDbContext db,
        SemanticKernelService kernelService,
        ILogger<ComparativeAnalysisService> logger)
    {
        _db = db;
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Compares two arguments and persists an ArgumentComparison record.
    /// </summary>
    public async Task<ArgumentComparison> CompareAsync(
        int argumentAId,
        int argumentBId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing arguments {A} and {B}", argumentAId, argumentBId);

        // ── Load both arguments with full decomposition ───────────────────────
        var argA = await LoadArgumentAsync(argumentAId, cancellationToken)
            ?? throw new InvalidOperationException($"Argument {argumentAId} not found.");
        var argB = await LoadArgumentAsync(argumentBId, cancellationToken)
            ?? throw new InvalidOperationException($"Argument {argumentBId} not found.");

        // ── Single LLM call for full comparison ───────────────────────────────
        var result = await RunComparisonAsync(argA, argB, cancellationToken);

        // ── Persist ────────────────────────────────────────────────────────────
        // Delete any existing comparison between these two arguments
        var existing = await _db.ArgumentComparisons
            .Where(c => (c.ArgumentAId == argumentAId && c.ArgumentBId == argumentBId)
                     || (c.ArgumentAId == argumentBId && c.ArgumentBId == argumentAId))
            .ToListAsync(cancellationToken);
        _db.ArgumentComparisons.RemoveRange(existing);

        var comparison = new ArgumentComparison
        {
            ArgumentAId = argumentAId,
            ArgumentBId = argumentBId,
            ConflictingPremisesJson = JsonSerializer.Serialize(result.ConflictingPremises, _jsonOpts),
            ComplementaryPremisesJson = JsonSerializer.Serialize(result.ComplementaryPremises, _jsonOpts),
            UniqueToPremisesAJson = JsonSerializer.Serialize(result.UniqueToPremisesA, _jsonOpts),
            UniqueToPremisesBJson = JsonSerializer.Serialize(result.UniqueToPremisesB, _jsonOpts),
            SynthesisNarrative = result.SynthesisNarrative,
            NetDirection = result.NetDirection,
            NetConfidence = result.NetConfidence,
            CreatedAt = DateTime.UtcNow
        };

        _db.ArgumentComparisons.Add(comparison);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Comparison complete: {Direction} ({Confidence:P0}), {Conflicts} conflicts",
            result.NetDirection, result.NetConfidence, result.ConflictingPremises.Count);

        return comparison;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Argument?> LoadArgumentAsync(int id, CancellationToken ct)
        => await _db.Arguments
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Assumptions)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Rebuttals)
            .Include(a => a.AdjudicationSummary)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    private async Task<ComparisonResult> RunComparisonAsync(
        Argument argA, Argument argB, CancellationToken ct)
    {
        var kernel = _kernelService.GetKernel();

        var premisesA = argA.Claims.SelectMany(c => c.Premises)
            .Select(p =>
            {
                var conf = p.ProvisionalConfidence.HasValue
                    ? $" [AI confidence: {p.ProvisionalConfidence:P0}]"
                    : "";
                return $"- {p.Text}{conf}";
            });

        var premisesB = argB.Claims.SelectMany(c => c.Premises)
            .Select(p =>
            {
                var conf = p.ProvisionalConfidence.HasValue
                    ? $" [AI confidence: {p.ProvisionalConfidence:P0}]"
                    : "";
                return $"- {p.Text}{conf}";
            });

        var assumptionsA = argA.Claims.SelectMany(c => c.Assumptions)
            .Select(a => $"- {a.Text} (critical: {(a.IsCritical ? "yes" : "no")})");
        var assumptionsB = argB.Claims.SelectMany(c => c.Assumptions)
            .Select(a => $"- {a.Text} (critical: {(a.IsCritical ? "yes" : "no")})");

        var rebuttalsA = argA.Claims.SelectMany(c => c.Rebuttals)
            .Select(r => $"- {r.Text} (strength: {r.Strength})");
        var rebuttalsB = argB.Claims.SelectMany(c => c.Rebuttals)
            .Select(r => $"- {r.Text} (strength: {r.Strength})");

        var confA = argA.AdjudicationSummary?.OverallConfidence;
        var confB = argB.AdjudicationSummary?.OverallConfidence;

        var prompt = $$$"""
        You are a senior epistemologist conducting a comparative analysis of two opposing arguments.

        ══════════════════════════════════════════
        ARGUMENT A: "{{{argA.Title}}}"
        Overall AI confidence: {{{(confA.HasValue ? confA.Value.ToString("P0") : "not computed")}}}

        Premises:
        {{{string.Join("\n", premisesA)}}}

        Key assumptions:
        {{{string.Join("\n", assumptionsA)}}}

        Rebuttals identified:
        {{{string.Join("\n", rebuttalsA)}}}

        ══════════════════════════════════════════
        ARGUMENT B: "{{{argB.Title}}}"
        Overall AI confidence: {{{(confB.HasValue ? confB.Value.ToString("P0") : "not computed")}}}

        Premises:
        {{{string.Join("\n", premisesB)}}}

        Key assumptions:
        {{{string.Join("\n", assumptionsB)}}}

        Rebuttals identified:
        {{{string.Join("\n", rebuttalsB)}}}

        ══════════════════════════════════════════

        Produce a structured comparative analysis using the EXACT section formats below.
        Do NOT add commentary outside the tagged lines.

        ═══ SECTION 1: CONFLICTS ═══
        List premise pairs that DIRECTLY CONTRADICT each other (one from each argument).
        Format — one per conflict, THREE pipe-delimited fields on ONE line:
        CONFLICT: [premise from A] | [premise from B] | [one sentence explaining the contradiction]

        ═══ SECTION 2: SHARED GROUND ═══
        List propositions or assumptions that BOTH arguments rely on or agree with.
        Format — one per line:
        SHARED: [the shared assertion]

        ═══ SECTION 3: UNIQUE TO A ═══
        Premises or claims found ONLY in Argument A and not addressed by Argument B.
        Format — one per line:
        UNIQUE_A: [premise text]

        ═══ SECTION 4: UNIQUE TO B ═══
        Premises or claims found ONLY in Argument B and not addressed by Argument A.
        Format — one per line:
        UNIQUE_B: [premise text]

        ═══ SECTION 5: NET ASSESSMENT ═══
        On the first line, state the net direction and confidence score:
        NET_DIRECTION: [FavoursA | FavoursB | Balanced | Insufficient] | NET_CONFIDENCE: [0.0-1.0]

        Then write a synthesis narrative (3–5 paragraphs of flowing prose) explaining:
        1. The core epistemic tension between the two positions
        2. Which premises hold up better across both arguments and why
        3. What the conflicting premises reveal about where evidence is genuinely uncertain
        4. Which direction the overall weight of argument and evidence favours, and why
        5. What new evidence or resolution would move this from contested to settled
        """;

        try
        {
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            return ParseComparisonResponse(result.ToString().Trim(), argA.Id, argB.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Comparative analysis LLM call failed");
            return new ComparisonResult
            {
                ArgumentAId = argA.Id,
                ArgumentBId = argB.Id,
                NetDirection = NetDirection.Insufficient,
                SynthesisNarrative = "Analysis could not be completed: " + ex.Message
            };
        }
    }

    private static ComparisonResult ParseComparisonResponse(string raw, int aId, int bId)
    {
        var result = new ComparisonResult { ArgumentAId = aId, ArgumentBId = bId };

        // Extract the NET_DIRECTION / NET_CONFIDENCE line before iterating the rest
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("NET_DIRECTION:", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Split('|');
            var dir = parts[0].Replace("NET_DIRECTION:", "", StringComparison.OrdinalIgnoreCase).Trim();
            result.NetDirection = dir.ToLowerInvariant() switch
            {
                "favoursa" => NetDirection.FavoursA,
                "favoursb" => NetDirection.FavoursB,
                "balanced" => NetDirection.Balanced,
                _ => NetDirection.Insufficient
            };

            if (parts.Length > 1)
            {
                var confStr = parts[1].Replace("NET_CONFIDENCE:", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (double.TryParse(confStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var conf))
                    result.NetConfidence = Math.Clamp(conf, 0.0, 1.0);
            }
            break;
        }

        // Parse tagged lines
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line["CONFLICT:".Length..].Split('|');
                if (parts.Length >= 3)
                    result.ConflictingPremises.Add(new ConflictingPremisePair
                    {
                        PremiseA = parts[0].Trim(),
                        PremiseB = parts[1].Trim(),
                        Explanation = parts[2].Trim()
                    });
                continue;
            }

            if (line.StartsWith("SHARED:", StringComparison.OrdinalIgnoreCase))
            {
                var text = line["SHARED:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(text)) result.ComplementaryPremises.Add(text);
                continue;
            }

            if (line.StartsWith("UNIQUE_A:", StringComparison.OrdinalIgnoreCase))
            {
                var text = line["UNIQUE_A:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(text)) result.UniqueToPremisesA.Add(text);
                continue;
            }

            if (line.StartsWith("UNIQUE_B:", StringComparison.OrdinalIgnoreCase))
            {
                var text = line["UNIQUE_B:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(text)) result.UniqueToPremisesB.Add(text);
                continue;
            }
        }

        // Extract synthesis narrative — everything after "NET_DIRECTION:" line's trailing content
        var netIdx = raw.IndexOf("NET_DIRECTION:", StringComparison.OrdinalIgnoreCase);
        if (netIdx >= 0)
        {
            // Skip to the end of the NET_DIRECTION line
            var endOfLine = raw.IndexOf('\n', netIdx);
            if (endOfLine >= 0)
            {
                var narrative = raw[(endOfLine + 1)..].Trim();
                // Strip any remaining section headers
                narrative = System.Text.RegularExpressions.Regex.Replace(
                    narrative, @"[═]+\s*SECTION.*?[═]+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(narrative))
                    result.SynthesisNarrative = narrative;
            }
        }

        return result;
    }
}
