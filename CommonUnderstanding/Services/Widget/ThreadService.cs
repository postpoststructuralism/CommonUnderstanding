using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Models.Widget;
using CommonUnderstanding.Models.Widget.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Widget;

/// <summary>
/// Core service for managing comment threads and their arguments.
/// </summary>
public class ThreadService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<ThreadService> _logger;

    public ThreadService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<ThreadService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>Get or create a thread for a given page URL on a site.</summary>
    public async Task<CommentThread> GetOrCreateThreadAsync(
        Guid siteId, string pageUrl, string? pageTitle, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var slug = NormalizeUrl(pageUrl);
        var thread = await db.CommentThreads
            .FirstOrDefaultAsync(t => t.SiteId == siteId && t.ThreadSlug == slug, ct);

        if (thread != null)
        {
            if (pageTitle != null && thread.PageTitle != pageTitle)
            {
                thread.PageTitle = pageTitle;
                thread.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return thread;
        }

        thread = new CommentThread
        {
            SiteId = siteId,
            PageUrl = pageUrl,
            PageTitle = pageTitle,
            ThreadSlug = slug,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.CommentThreads.Add(thread);
        await db.SaveChangesAsync(ct);
        return thread;
    }

    /// <summary>Post a new comment (SocialArgument) to a thread.</summary>
    public async Task<SocialArgument?> PostCommentAsync(
        Guid threadId, string userId, string content, string? parentArgumentId,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var thread = await db.CommentThreads.FindAsync(new object[] { threadId }, ct);
        if (thread == null || thread.IsLocked)
            return null;

        var argument = new SocialArgument
        {
            Title = content.Length > 300 ? content[..297] + "..." : content,
            WarrantText = content,
            UserId = userId,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Tags = new[] { "widget_comment" }
        };

        db.SocialArguments.Add(argument);
        await db.SaveChangesAsync(ct);

        var threadArg = new ThreadArgument
        {
            ThreadId = threadId,
            ArgumentId = argument.Id,
            IsTopLevel = string.IsNullOrEmpty(parentArgumentId),
            CreatedAt = DateTime.UtcNow
        };
        db.ThreadArguments.Add(threadArg);

        // If it's a reply, create an ArgumentLink
        if (!string.IsNullOrEmpty(parentArgumentId) && Guid.TryParse(parentArgumentId, out var parentId))
        {
            var link = new ArgumentLink
            {
                SourceArgumentId = argument.Id,
                TargetArgumentId = parentId,
                LinkType = LinkType.Reply,
                CreatedAt = DateTime.UtcNow
            };
            db.ArgumentLinks.Add(link);
        }

        thread.TotalComments++;
        thread.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return argument;
    }

    /// <summary>Fetch comments for a thread with pagination and sorting.</summary>
    public async Task<List<CommentDto>> GetCommentsAsync(
        Guid threadId, string sort = "hot", int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.ThreadArguments
            .Where(ta => ta.ThreadId == threadId)
            .Include(ta => ta.Argument)
            .ThenInclude(a => a.Votes)
            .Select(ta => ta.Argument);

        // Apply sorting
        query = sort.ToLowerInvariant() switch
        {
            "new" => query.OrderByDescending(a => a.CreatedAt),
            "top" => query.OrderByDescending(a => a.UpvoteCount),
            "controversial" => query.OrderByDescending(a => a.ControversyScore),
            _ => query.OrderByDescending(a => a.HotScore) // "hot" default
        };

        var arguments = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        // Get parent links for reply detection
        var argIds = arguments.Select(a => a.Id).ToList();
        var parentLinks = await db.ArgumentLinks
            .Where(l => argIds.Contains(l.SourceArgumentId) && l.LinkType == LinkType.Reply)
            .ToDictionaryAsync(l => l.SourceArgumentId, l => (Guid?)l.TargetArgumentId, ct);

        return arguments.Select(a => new CommentDto(
            Id: a.Id.ToString(),
            AuthorName: a.UserId, // Will be enriched by caller
            Content: a.WarrantText,
            Upvotes: a.UpvoteCount,
            Downvotes: a.DownvoteCount,
            ReplyCount: a.ReplyCount,
            WilsonScore: a.WilsonScore,
            CreatedAt: a.CreatedAt,
            ParentId: parentLinks.TryGetValue(a.Id, out var pid) ? pid?.ToString() : null,
            IsDeleted: a.IsShadowBanned
        )).ToList();
    }

    /// <summary>Get thread metadata.</summary>
    public async Task<ThreadDto?> GetThreadAsync(Guid threadId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var thread = await db.CommentThreads
            .Include(t => t.ThreadArguments)
            .FirstOrDefaultAsync(t => t.Id == threadId, ct);

        if (thread == null) return null;

        var comments = await GetCommentsAsync(threadId, thread.SortOrder, ct: ct);

        return new ThreadDto(
            ThreadId: thread.Id.ToString(),
            PageUrl: thread.PageUrl,
            PageTitle: thread.PageTitle,
            TotalComments: thread.TotalComments,
            IsLocked: thread.IsLocked,
            SortOrder: thread.SortOrder,
            Comments: comments
        );
    }

    /// <summary>Lock/unlock a thread.</summary>
    public async Task<bool> SetThreadLockAsync(Guid threadId, bool locked, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var thread = await db.CommentThreads.FindAsync(new object[] { threadId }, ct);
        if (thread == null) return false;
        thread.IsLocked = locked;
        thread.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Delete (shadow-ban) a comment.</summary>
    public async Task<bool> DeleteCommentAsync(Guid argumentId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var arg = await db.SocialArguments.FindAsync(new object[] { argumentId }, ct);
        if (arg == null) return false;
        arg.IsShadowBanned = true;
        arg.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizeUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.PathAndQuery.TrimEnd('/').ToLowerInvariant();
        }
        catch
        {
            return url.TrimEnd('/').ToLowerInvariant();
        }
    }
}