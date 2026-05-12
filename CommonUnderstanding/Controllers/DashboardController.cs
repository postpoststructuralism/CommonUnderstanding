using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CommonUnderstandingService _cuService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ApplicationDbContext db,
        CommonUnderstandingService cuService,
        ILogger<DashboardController> logger)
    {
        _db = db;
        _cuService = cuService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // All arguments with adjudication + evidence counts
        var arguments = await _db.Arguments
            .Include(a => a.AdjudicationSummary)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
                    .ThenInclude(p => p.EvidenceItems)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ToListAsync();

        // Graph stats
        GraphStatistics? graphStats = null;
        try { graphStats = await _cuService.GetStatisticsAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to load graph statistics"); }

        // Build attention items
        var attentionItems = BuildAttentionItems(arguments);

        // Recent activity (last 10 events across all entity types)
        var recentActivity = await BuildRecentActivityAsync();

        // Stakeholder counts per argument
        var stakeholderCounts = await _db.StakeholderPositions
            .GroupBy(p => p.ArgumentId)
            .Select(g => new { ArgumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ArgumentId, x => x.Count);

        ViewBag.GraphStats       = graphStats;
        ViewBag.AttentionItems   = attentionItems;
        ViewBag.RecentActivity   = recentActivity;
        ViewBag.StakeholderCounts = stakeholderCounts;

        return View(arguments);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<AttentionItem> BuildAttentionItems(List<Argument> arguments)
    {
        var items = new List<AttentionItem>();

        foreach (var arg in arguments.Where(a => a.Status == ArgumentStatus.Complete))
        {
            var allPremises = arg.Claims.SelectMany(c => c.Premises).ToList();

            // No evidence on any premise
            int unevidenced = allPremises.Count(p => p.EvidenceItems.Count == 0);
            if (unevidenced > 0)
                items.Add(new AttentionItem
                {
                    ArgumentId = arg.Id,
                    ArgumentTitle = arg.Title,
                    Type = AttentionItemType.EvidenceGap,
                    Message = $"{unevidenced} premise(s) have no evidence",
                    Icon = "bi-exclamation-triangle-fill",
                    Color = "warning"
                });

            // Contested premises
            int contested = allPremises.Count(p => p.Status == PropositionStatus.Contested);
            if (contested > 0)
                items.Add(new AttentionItem
                {
                    ArgumentId = arg.Id,
                    ArgumentTitle = arg.Title,
                    Type = AttentionItemType.ContestedPremise,
                    Message = $"{contested} contested premise(s)",
                    Icon = "bi-shield-exclamation",
                    Color = "danger"
                });
        }

        // Draft arguments not yet analyzed
        foreach (var arg in arguments.Where(a => a.Status == ArgumentStatus.Draft))
        {
            items.Add(new AttentionItem
            {
                ArgumentId = arg.Id,
                ArgumentTitle = arg.Title,
                Type = AttentionItemType.PendingAnalysis,
                Message = "Not yet analyzed",
                Icon = "bi-hourglass",
                Color = "secondary"
            });
        }

        return items.Take(10).ToList();
    }

    private async Task<List<ActivityEvent>> BuildRecentActivityAsync()
    {
        var events = new List<ActivityEvent>();

        // Arguments submitted
        var recentArgs = await _db.Arguments
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new { a.Id, a.Title, a.CreatedAt, a.SubmittedBy })
            .ToListAsync();

        foreach (var a in recentArgs)
            events.Add(new ActivityEvent
            {
                ArgumentId = a.Id,
                Description = $"Argument \"{TruncateText(a.Title, 50)}\" submitted",
                Detail = string.IsNullOrWhiteSpace(a.SubmittedBy) ? null : $"by {a.SubmittedBy}",
                Timestamp = a.CreatedAt,
                Icon = "bi-file-plus",
                Color = "primary"
            });

        // Evidence added
        var recentEvidence = await _db.EvidenceItems
            .Include(e => e.Proposition)
                .ThenInclude(p => p!.Claim)
                    .ThenInclude(c => c!.Argument)
            .OrderByDescending(e => e.AddedAt)
            .Take(5)
            .ToListAsync();

        foreach (var ev in recentEvidence)
            events.Add(new ActivityEvent
            {
                ArgumentId = ev.Proposition?.Claim?.ArgumentId,
                Description = $"Evidence added: \"{TruncateText(ev.Citation, 50)}\"",
                Detail = ev.AddedBy != null ? $"by {ev.AddedBy}" : null,
                Timestamp = ev.AddedAt,
                Icon = "bi-journal-plus",
                Color = "info"
            });

        // Stakeholder positions
        var recentPositions = await _db.StakeholderPositions
            .Include(p => p.StakeholderRef)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        foreach (var pos in recentPositions)
        {
            var name = pos.IsAnonymous ? "Anonymous" : pos.StakeholderRef?.Name ?? "Unknown";
            events.Add(new ActivityEvent
            {
                ArgumentId = pos.ArgumentId,
                Description = $"{name} registered position: {pos.Position}",
                Timestamp = pos.CreatedAt,
                Icon = "bi-person-check",
                Color = "success"
            });
        }

        // Adjudication completed
        var recentAdj = await _db.AdjudicationSummaries
            .Include(s => s.Argument)
            .OrderByDescending(s => s.ComputedAt)
            .Take(5)
            .ToListAsync();

        foreach (var adj in recentAdj)
            events.Add(new ActivityEvent
            {
                ArgumentId = adj.ArgumentId,
                Description = $"Adjudication: {adj.Recommendation} — {adj.OverallConfidence:P0} confidence",
                Detail = adj.Argument != null ? TruncateText(adj.Argument.Title, 40) : null,
                Timestamp = adj.ComputedAt,
                Icon = "bi-graph-up-arrow",
                Color = adj.Recommendation == DecisionRecommendation.Proceed ? "success"
                       : adj.Recommendation == DecisionRecommendation.Reject  ? "danger" : "warning"
            });

        return events
            .OrderByDescending(e => e.Timestamp)
            .Take(12)
            .ToList();
    }

    private static string TruncateText(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}

// ─────────────────────────────────────────────
//  Dashboard DTOs
// ─────────────────────────────────────────────

public enum AttentionItemType { EvidenceGap, ContestedPremise, PendingAnalysis, StakeholderDeadlock }

public class AttentionItem
{
    public int ArgumentId { get; set; }
    public string ArgumentTitle { get; set; } = string.Empty;
    public AttentionItemType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-exclamation";
    public string Color { get; set; } = "warning";
}

public class ActivityEvent
{
    public int? ArgumentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = "bi-clock";
    public string Color { get; set; } = "secondary";
}
