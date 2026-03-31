using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using System.Text;
using System.Text.Json;

namespace CommonUnderstanding.Controllers;

public class EmergentConclusionsController : Controller
{
    private readonly EmergentConclusionsEngine _engine;
    private readonly ILogger<EmergentConclusionsController> _logger;

    public EmergentConclusionsController(
        EmergentConclusionsEngine engine,
        ILogger<EmergentConclusionsController> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    // ── GET /EmergentConclusions ──────────────────────────────────────────────
    // Standard (graph-only) analysis. Auto-persists every result.

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        try
        {
            var report = await _engine.GenerateReportAsync(ct: ct);

            if (report.HasSufficientData)
                await _engine.PersistReportAsync(report, ct);

            return View(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating emergent conclusions report");
            TempData["Error"] = "An error occurred while generating the emergent conclusions report.";
            return View("Index", new EmergentConclusionsReport
            {
                HasSufficientData = false,
                InsufficientDataReason = "An unexpected error occurred. Please try again."
            });
        }
    }

    // ── GET /EmergentConclusions/DeepAnalysis ─────────────────────────────────
    // Shows the loading page; JS on that page opens the SSE stream.

    [HttpGet]
    public IActionResult DeepAnalysis() => View("RunningAnalysis");

    // ── GET /EmergentConclusions/DeepAnalyzeStream ────────────────────────────
    // SSE endpoint: runs the full deep analysis, streams progress, persists result,
    // then emits a complete event with a redirect URL to ShowDeepReport/{id}.

    [HttpGet]
    public async Task DeepAnalyzeStream()
    {
        var ct = HttpContext.RequestAborted;

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        async Task SendAsync(string eventType, object data)
        {
            var json = JsonSerializer.Serialize(data);
            await Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        try
        {
            EmergentConclusionsReport? report = null;

            report = await _engine.GenerateReportAsync(
                deep: true,
                onProgress: async (label, step, total) =>
                    await SendAsync("progress", new { step, total, label }),
                ct: ct);

            int snapshotId = 0;
            if (report.HasSufficientData)
            {
                await SendAsync("progress", new { step = 7, total = 7, label = "Saving analysis snapshot…" });
                snapshotId = await _engine.PersistReportAsync(report, ct);
            }

            var redirectUrl = snapshotId > 0
                ? Url.Action(nameof(ShowDeepReport), new { id = snapshotId })
                : Url.Action(nameof(Index));

            await SendAsync("complete", new { redirectUrl });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Deep analysis stream canceled (client disconnected)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deep analysis stream failed");
            try
            {
                await SendAsync("error", new { message = $"Deep analysis failed: {ex.Message}" });
            }
            catch { /* client already gone */ }
        }
    }

    // ── GET /EmergentConclusions/ShowDeepReport/{id} ──────────────────────────
    // Loads the persisted full JSON for a deep analysis snapshot and
    // re-renders the Index view with that data (no re-analysis needed).

    [HttpGet]
    public async Task<IActionResult> ShowDeepReport(int id, CancellationToken ct = default)
    {
        var report = await _engine.LoadPersistedReportAsync(id, ct);
        if (report == null)
        {
            TempData["Error"] = "Could not load the analysis report. The snapshot may be incomplete.";
            return RedirectToAction(nameof(Index));
        }
        TempData["Success"] = "Deep analysis complete — results loaded from saved snapshot.";
        return View("Index", report);
    }

    // ── GET /EmergentConclusions/History ─────────────────────────────────────

    public async Task<IActionResult> History(CancellationToken ct)
    {
        var history = await _engine.GetHistoryAsync(ct);
        return View(history);
    }

    // ── GET /EmergentConclusions/Export ──────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Export(bool deep = false, CancellationToken ct = default)
    {
        try
        {
            var report = await _engine.GenerateReportAsync(deep, ct: ct);
            var md = BuildMarkdownExport(report);
            var bytes = Encoding.UTF8.GetBytes(md);
            var filename = $"emergent-conclusions-{report.GeneratedAt:yyyy-MM-dd-HHmm}.md";
            return File(bytes, "text/markdown; charset=utf-8", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating export");
            TempData["Error"] = "Export failed. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ── POST /EmergentConclusions/GenerateDialogue ────────────────────────────

    [HttpPost]
    public IActionResult GenerateDialogue([FromBody] DialogueRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.HarmonyTitle))
            return BadRequest();

        var guide = new
        {
            title = $"Dialogue Guide: {req.HarmonyTitle}",
            steps = new[]
            {
                new { step = 1, label = "Open with shared ground", text = $"Begin by explicitly naming the harmony: \"{req.HarmonyTitle}\". Ask each participant to confirm they recognise this as common ground." },
                new { step = 2, label = "Acknowledge divergence", text = "Invite participants to articulate where they still disagree, using the shared ground as the baseline." },
                new { step = 3, label = "Explore the bridge", text = string.IsNullOrEmpty(req.OpportunityDescription) ? "Ask: What would it take for us to move from shared premises to a shared conclusion?" : req.OpportunityDescription },
                new { step = 4, label = "Define next action", text = "Agree on one concrete follow-up: an evidence request, a joint sub-argument, or a stakeholder position update." }
            }
        };
        return Ok(guide);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildMarkdownExport(EmergentConclusionsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Emergent Conclusions Report");
        sb.AppendLine($"*Generated: {report.GeneratedAt:dddd, MMMM d, yyyy HH:mm} UTC*");
        sb.AppendLine($"*Analysis mode: {(report.IsDeepAnalysis ? "Deep (LLM-enhanced)" : "Standard (graph-only)")}*");
        sb.AppendLine();

        sb.AppendLine("## Graph Health");
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine($"| Arguments | {report.GraphHealth.TotalArguments} |");
        sb.AppendLine($"| Propositions | {report.GraphHealth.TotalPropositions} |");
        sb.AppendLine($"| Evidence Items | {report.GraphHealth.TotalEvidenceItems} |");
        sb.AppendLine($"| Evidence Coverage | {report.GraphHealth.EvidenceCoveragePercent:0}% |");
        sb.AppendLine($"| Average Confidence | {report.GraphHealth.AverageConfidence:P0} |");
        sb.AppendLine($"| Settled | {report.GraphHealth.SettledCount} |");
        sb.AppendLine($"| Contested | {report.GraphHealth.ContestedCount} |");
        sb.AppendLine($"| Untested Critical Assumptions | {report.GraphHealth.CriticalAssumptionsUntested} |");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(report.ExecutiveSummary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine(report.ExecutiveSummary);
            sb.AppendLine();
        }

        sb.AppendLine("## Blindspots");
        if (!report.Blindspots.Any())
        {
            sb.AppendLine("*No blindspots detected.*");
        }
        else
        {
            foreach (var b in report.Blindspots)
            {
                sb.AppendLine($"### {b.Title}");
                sb.AppendLine($"**Category:** {b.Category}  |  **Significance:** {b.Significance:P0}  |  **Confidence:** {b.Confidence:P0}");
                sb.AppendLine();
                sb.AppendLine(b.Description);
                if (!string.IsNullOrEmpty(b.SuggestedAction))
                {
                    sb.AppendLine();
                    sb.AppendLine($"> **Suggested action:** {b.SuggestedAction}");
                }
                if (b.InvolvedArgumentTitles.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Affected arguments:** {string.Join(", ", b.InvolvedArgumentTitles.Select(t => $"_{t}_"))}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Harmonies");
        if (!report.Harmonies.Any())
        {
            sb.AppendLine("*No harmonies detected.*");
        }
        else
        {
            foreach (var h in report.Harmonies)
            {
                sb.AppendLine($"### {h.Title}");
                sb.AppendLine($"**Category:** {h.Category}  |  **Significance:** {h.Significance:P0}  |  **Confidence:** {h.Confidence:P0}");
                sb.AppendLine();
                sb.AppendLine(h.Description);
                if (!string.IsNullOrEmpty(h.OpportunityDescription))
                {
                    sb.AppendLine();
                    sb.AppendLine($"> **Opportunity:** {h.OpportunityDescription}");
                }
                if (h.InvolvedArgumentTitles.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Related arguments:** {string.Join(", ", h.InvolvedArgumentTitles.Select(t => $"_{t}_"))}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

public class DialogueRequest
{
    public string? HarmonyTitle { get; set; }
    public string? OpportunityDescription { get; set; }
}
