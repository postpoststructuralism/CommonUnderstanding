using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Orchestrates the Emergent Conclusions analysis pipeline.
/// Calls BlindspotDetector and HarmonyDetector, assembles the full report,
/// and computes graph health statistics.
/// </summary>
public class EmergentConclusionsEngine
{
    private const int MinimumArgumentsRequired = 2;

    private readonly ApplicationDbContext _db;
    private readonly BlindspotDetector _blindspotDetector;
    private readonly HarmonyDetector _harmonyDetector;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<EmergentConclusionsEngine> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public EmergentConclusionsEngine(
        ApplicationDbContext db,
        BlindspotDetector blindspotDetector,
        HarmonyDetector harmonyDetector,
        SemanticKernelService kernelService,
        ILogger<EmergentConclusionsEngine> logger)
    {
        _db = db;
        _blindspotDetector = blindspotDetector;
        _harmonyDetector = harmonyDetector;
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a full emergent conclusions report.
    /// When <paramref name="deep"/> is true, LLM-powered detectors are also run.
    /// Supply <paramref name="onProgress"/> to receive step-by-step updates (label, step, total);
    /// when a callback is provided the detectors run sequentially so each step is meaningful.
    /// Without a callback the detectors run in parallel for maximum throughput.
    /// </summary>
    public async Task<EmergentConclusionsReport> GenerateReportAsync(
        bool deep = false,
        Func<string, int, int, Task>? onProgress = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating emergent conclusions report (deep={Deep})...", deep);

        int totalSteps = deep ? 6 : 3;

        if (onProgress != null)
            await onProgress("Computing graph statistics…", 1, totalSteps);

        var health = await ComputeGraphHealthAsync(ct);
        var report = new EmergentConclusionsReport
        {
            GraphHealth = health,
            GeneratedAt = DateTime.UtcNow,
            IsDeepAnalysis = deep
        };

        if (health.TotalArguments < MinimumArgumentsRequired)
        {
            report.HasSufficientData = false;
            report.InsufficientDataReason =
                $"The analysis requires at least {MinimumArgumentsRequired} arguments with " +
                $"decomposed propositions. Currently there are {health.TotalArguments} argument(s). " +
                $"Submit and decompose more arguments to enable emergent conclusions.";
            return report;
        }

        List<EmergentConclusion> blindspots;
        List<EmergentConclusion> harmonies;

        if (onProgress != null)
        {
            // ── Sequential path: fine-grained progress reporting ─────────────
            if (deep)
            {
                await onProgress("Detecting blindspots — graph analysis…", 2, totalSteps);
                var bs1 = await _blindspotDetector.DetectAllAsync(ct);

                await onProgress("Detecting blindspots — AI semantic analysis…", 3, totalSteps);
                var bs2 = await _blindspotDetector.DetectSilentContradictionsAsync(ct);
                blindspots = bs1.Concat(bs2).ToList();

                await onProgress("Detecting harmonies — graph analysis…", 4, totalSteps);
                var h1 = await _harmonyDetector.DetectAllAsync(ct);

                await onProgress("Detecting harmonies — AI semantic analysis…", 5, totalSteps);
                var h2a = await _harmonyDetector.ExtractSharedValueCoreAsync(ct);
                var h2b = await _harmonyDetector.DetectCrossDomainReinforcementAsync(ct);
                harmonies = h1.Concat(h2a).Concat(h2b).ToList();
            }
            else
            {
                await onProgress("Scanning for blindspots…", 2, totalSteps);
                blindspots = await _blindspotDetector.DetectAllAsync(ct);

                await onProgress("Detecting harmonies…", 3, totalSteps);
                harmonies = await _harmonyDetector.DetectAllAsync(ct);
            }
        }
        else
        {
            // ── Parallel path: maximum throughput ────────────────────────────
            var blindspotTask = deep
                ? _blindspotDetector.DetectAllDeepAsync(ct)
                : _blindspotDetector.DetectAllAsync(ct);
            var harmonyTask = deep
                ? _harmonyDetector.DetectAllDeepAsync(ct)
                : _harmonyDetector.DetectAllAsync(ct);

            await Task.WhenAll(blindspotTask, harmonyTask);
            blindspots = blindspotTask.Result;
            harmonies  = harmonyTask.Result;
        }

        report.Blindspots = blindspots.OrderByDescending(b => b.Significance).ToList();
        report.Harmonies  = harmonies.OrderByDescending(h => h.Significance).ToList();
        report.HasSufficientData = true;

        if (deep && (report.Blindspots.Any() || report.Harmonies.Any()))
        {
            if (onProgress != null)
                await onProgress("Generating executive summary…", 6, totalSteps);
            report.ExecutiveSummary = await GenerateExecutiveSummaryAsync(report, ct);
        }

        _logger.LogInformation(
            "Emergent conclusions report complete: {Blindspots} blindspots, {Harmonies} harmonies",
            report.Blindspots.Count, report.Harmonies.Count);

        return report;
    }

    /// <summary>
    /// Attempts to reconstruct an <see cref="EmergentConclusionsReport"/> from
    /// the FullReportJson stored in a persisted snapshot. Returns null if the
    /// snapshot doesn't exist or contains no full JSON.
    /// </summary>
    public async Task<EmergentConclusionsReport?> LoadPersistedReportAsync(
        int id, CancellationToken ct = default)
    {
        var snapshot = await _db.PersistedEmergentReports.FindAsync([id], ct);
        if (snapshot?.FullReportJson == null) return null;
        try
        {
            return JsonSerializer.Deserialize<EmergentConclusionsReport>(snapshot.FullReportJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize FullReportJson for snapshot {Id}", id);
            return null;
        }
    }

    /// <summary>Persists a snapshot of the report for historical tracking.</summary>
    public async Task<int> PersistReportAsync(EmergentConclusionsReport report, CancellationToken ct = default)
    {
        var blindspotTitles = report.Blindspots
            .Select(b => new { b.Title, Category = b.Category.ToString(), b.Significance })
            .ToList();
        var harmonyTitles = report.Harmonies
            .Select(h => new { h.Title, Category = h.Category.ToString(), h.Significance })
            .ToList();

        var snapshot = new PersistedEmergentReport
        {
            GeneratedAt = report.GeneratedAt,
            IsDeepAnalysis = report.IsDeepAnalysis,
            TotalArguments = report.GraphHealth.TotalArguments,
            TotalPropositions = report.GraphHealth.TotalPropositions,
            TotalEvidenceItems = report.GraphHealth.TotalEvidenceItems,
            AverageConfidence = report.GraphHealth.AverageConfidence,
            SettledCount = report.GraphHealth.SettledCount,
            ContestedCount = report.GraphHealth.ContestedCount,
            BlindspotCount = report.Blindspots.Count,
            HarmonyCount = report.Harmonies.Count,
            CriticalAssumptionsUntested = report.GraphHealth.CriticalAssumptionsUntested,
            BlindspotsSummaryJson = JsonSerializer.Serialize(blindspotTitles, _jsonOpts),
            HarmoniesSummaryJson = JsonSerializer.Serialize(harmonyTitles, _jsonOpts),
            ExecutiveSummary = report.ExecutiveSummary,
            FullReportJson = JsonSerializer.Serialize(report)
        };

        _db.PersistedEmergentReports.Add(snapshot);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Persisted emergent report snapshot (id={Id})", snapshot.Id);
        return snapshot.Id;
    }

    /// <summary>Returns historical report snapshots, newest first.</summary>
    public async Task<List<PersistedEmergentReport>> GetHistoryAsync(CancellationToken ct = default) =>
        await _db.PersistedEmergentReports
            .OrderByDescending(r => r.GeneratedAt)
            .Take(30)
            .ToListAsync(ct);

    // ─────────────────────────────────────────────────────────────────────────
    //  Executive Summary (LLM)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<string?> GenerateExecutiveSummaryAsync(
        EmergentConclusionsReport report, CancellationToken ct)
    {
        try
        {
            var kernel = _kernelService.GetKernel();

            var topBlindspots = report.Blindspots
                .Take(5)
                .Select(b => $"- [{b.Category}] {b.Title}: {b.Description[..Math.Min(150, b.Description.Length)]}");
            var topHarmonies = report.Harmonies
                .Take(5)
                .Select(h => $"- [{h.Category}] {h.Title}: {h.Description[..Math.Min(150, h.Description.Length)]}");

            var prompt = $"""
            You are the chief epistemologist for an evidence-based decision platform.
            You have just completed an emergent analysis of the community's argument inventory.

            GRAPH HEALTH:
            - Arguments: {report.GraphHealth.TotalArguments}
            - Propositions: {report.GraphHealth.TotalPropositions}
            - Evidence items: {report.GraphHealth.TotalEvidenceItems}
            - Average confidence: {report.GraphHealth.AverageConfidence:P0}
            - Settled: {report.GraphHealth.SettledCount}, Contested: {report.GraphHealth.ContestedCount}
            - Untested critical assumptions: {report.GraphHealth.CriticalAssumptionsUntested}

            TOP BLINDSPOTS DETECTED:
            {string.Join("\n", topBlindspots)}

            TOP HARMONIES DETECTED:
            {string.Join("\n", topHarmonies)}

            Write a concise executive summary (3-4 paragraphs) for community leaders that:
            1. Characterises the overall state of epistemic health of the community's argument inventory
            2. Highlights the most significant blindspot(s) and why they matter operationally
            3. Highlights the most significant harmony/harmonies and what opportunity they present
            4. Ends with a clear, prioritised recommendation for the community's next focus

            Write in authoritative but accessible prose. No bullet points or headers — flowing paragraphs only.
            """;

            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var summary = result.ToString().Trim();
            return string.IsNullOrWhiteSpace(summary) ? null : summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Executive summary generation failed");
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Graph Health
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<GraphHealthSummary> ComputeGraphHealthAsync(CancellationToken ct)
    {
        var totalArguments = await _db.Arguments.CountAsync(ct);
        var totalPropositions = await _db.Propositions.CountAsync(ct);
        var totalEvidenceItems = await _db.EvidenceItems.CountAsync(ct);
        var totalStakeholders = await _db.Stakeholders.CountAsync(ct);
        var totalComparisons = await _db.ArgumentComparisons.CountAsync(ct);

        // Graph node statistics
        var nodeStats = await _db.CommonUnderstandingNodes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Settled = g.Count(n => n.Status == PropositionStatus.Settled),
                Contested = g.Count(n => n.Status == PropositionStatus.Contested),
                Unknown = g.Count(n => n.Status == PropositionStatus.Unknown),
                Unevaluated = g.Count(n => n.Status == PropositionStatus.Unevaluated),
                AvgConfidence = g.Average(n => (double?)n.Confidence) ?? 0.5,
                WithEvidence = g.Count(n => n.EvidenceCount > 0)
            })
            .FirstOrDefaultAsync(ct);

        int totalNodes = nodeStats?.Total ?? 0;

        // Unsupported critical assumptions
        int criticalUntested = await _db.Assumptions
            .Where(a => a.IsCritical && !a.IsSupported)
            .CountAsync(ct);

        // High-strength rebuttals
        int highRebuttals = await _db.Rebuttals
            .Where(r => r.Strength == "high")
            .CountAsync(ct);

        double evidenceCoverage = totalNodes > 0
            ? Math.Round((double)(nodeStats?.WithEvidence ?? 0) / totalNodes * 100, 1)
            : 0;

        return new GraphHealthSummary
        {
            TotalArguments = totalArguments,
            TotalPropositions = totalPropositions,
            TotalEvidenceItems = totalEvidenceItems,
            TotalStakeholders = totalStakeholders,
            TotalComparisons = totalComparisons,
            AverageConfidence = Math.Round(nodeStats?.AvgConfidence ?? 0.5, 3),
            SettledCount = nodeStats?.Settled ?? 0,
            ContestedCount = nodeStats?.Contested ?? 0,
            UnknownCount = nodeStats?.Unknown ?? 0,
            UnevaluatedCount = nodeStats?.Unevaluated ?? 0,
            EvidenceCoveragePercent = evidenceCoverage,
            CriticalAssumptionsUntested = criticalUntested,
            HighStrengthRebuttals = highRebuttals
        };
    }
}
