using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Widget;
using CommonUnderstanding.Models.Widget.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Widget;

/// <summary>
/// Moderation service for the embeddable widget.
/// Handles AI flagging, manual review, and the moderation queue.
/// </summary>
public class WidgetModerationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<WidgetModerationService> _logger;

    public WidgetModerationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<WidgetModerationService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>Flag a comment for moderation review.</summary>
    public async Task<CommentModerationItem> FlagCommentAsync(
        Guid siteId, Guid argumentId, string reason, double? aiConfidence = null,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var item = new CommentModerationItem
        {
            SiteId = siteId,
            ArgumentId = argumentId,
            Status = "pending",
            FlagReason = reason,
            AiConfidence = aiConfidence,
            CreatedAt = DateTime.UtcNow
        };

        db.CommentModerationItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    /// <summary>Approve or reject a moderation item.</summary>
    public async Task<bool> ReviewItemAsync(
        Guid itemId, string reviewerUserId, bool approved,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var item = await db.CommentModerationItems.FindAsync(new object[] { itemId }, ct);
        if (item == null) return false;

        item.Status = approved ? "approved" : "rejected";
        item.ReviewedByUserId = reviewerUserId;
        item.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // If rejected, shadow-ban the comment
        if (!approved)
        {
            var arg = await db.SocialArguments.FindAsync(new object[] { item.ArgumentId }, ct);
            if (arg != null)
            {
                arg.IsShadowBanned = true;
                arg.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        return true;
    }

    /// <summary>Get the moderation queue for a site.</summary>
    public async Task<List<ModerationQueueItemDto>> GetQueueAsync(
        Guid siteId, string? status = null, int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.CommentModerationItems
            .Where(m => m.SiteId == siteId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(m => m.Status == status);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        // Get comment snippets
        var argIds = items.Select(m => m.ArgumentId).ToList();
        var args = await db.SocialArguments
            .Where(a => argIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.WarrantText, ct);

        return items.Select(m => new ModerationQueueItemDto(
            Id: m.Id.ToString(),
            CommentId: m.ArgumentId.ToString(),
            CommentSnippet: args.TryGetValue(m.ArgumentId, out var snippet)
                ? (snippet.Length > 200 ? snippet[..197] + "..." : snippet)
                : "[deleted]",
            Status: m.Status,
            FlagReason: m.FlagReason,
            AiConfidence: m.AiConfidence,
            CreatedAt: m.CreatedAt
        )).ToList();
    }

    /// <summary>Get pending moderation count for a site.</summary>
    public async Task<int> GetPendingCountAsync(Guid siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.CommentModerationItems
            .CountAsync(m => m.SiteId == siteId && m.Status == "pending", ct);
    }
}