using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Widget;
using CommonUnderstanding.Models.Widget.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Widget;

/// <summary>
/// Analytics and usage tracking for the embeddable widget.
/// </summary>
public class WidgetAnalyticsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<WidgetAnalyticsService> _logger;

    public WidgetAnalyticsService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<WidgetAnalyticsService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>Record a page view for a site (upserts daily usage).</summary>
    public async Task RecordPageViewAsync(Guid siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await UpsertUsageAsync(db, siteId, u => u.PageViews++, ct);
    }

    /// <summary>Record a comment posted.</summary>
    public async Task RecordCommentAsync(Guid siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await UpsertUsageAsync(db, siteId, u => u.CommentsPosted++, ct);
    }

    /// <summary>Record a vote cast.</summary>
    public async Task RecordVoteAsync(Guid siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await UpsertUsageAsync(db, siteId, u => u.VotesCast++, ct);
    }

    /// <summary>Record an AI analysis run.</summary>
    public async Task RecordAiAnalysisAsync(Guid siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await UpsertUsageAsync(db, siteId, u => u.AiAnalysesRun++, ct);
    }

    /// <summary>Get usage stats for a site over a date range.</summary>
    public async Task<List<UsageStatsDto>> GetUsageAsync(
        Guid siteId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var records = await db.WidgetUsages
            .Where(u => u.SiteId == siteId && u.Date >= from && u.Date <= to)
            .OrderBy(u => u.Date)
            .ToListAsync(ct);

        return records.Select(r => new UsageStatsDto(
            Date: r.Date,
            PageViews: r.PageViews,
            CommentsPosted: r.CommentsPosted,
            VotesCast: r.VotesCast,
            AiAnalysesRun: r.AiAnalysesRun
        )).ToList();
    }

    /// <summary>Get total usage across all sites for an owner.</summary>
    public async Task<long> GetTotalPageViewsAsync(
        string ownerUserId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var siteIds = await db.CommentSites
            .Where(s => s.OwnerUserId == ownerUserId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return await db.WidgetUsages
            .Where(u => siteIds.Contains(u.SiteId))
            .SumAsync(u => u.PageViews, ct);
    }

    private async Task UpsertUsageAsync(
        ApplicationDbContext db, Guid siteId,
        Action<WidgetUsage> updateAction,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var usage = await db.WidgetUsages
            .FirstOrDefaultAsync(u => u.SiteId == siteId && u.Date == today, ct);

        if (usage == null)
        {
            usage = new WidgetUsage
            {
                SiteId = siteId,
                Date = today
            };
            db.WidgetUsages.Add(usage);
        }

        updateAction(usage);
        await db.SaveChangesAsync(ct);
    }
}