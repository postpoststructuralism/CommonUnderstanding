using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Service for managing follow-up arguments (Twitter-like replies to arguments)
/// </summary>
public class FollowUpArgumentService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<FollowUpArgumentService> _logger;
    private readonly ArgumentValidationService _validationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SocialArgumentAnalysisService _analysisService;

    public FollowUpArgumentService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<FollowUpArgumentService> logger,
        ArgumentValidationService validationService,
        IHttpContextAccessor httpContextAccessor,
        SocialArgumentAnalysisService analysisService)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _validationService = validationService;
        _httpContextAccessor = httpContextAccessor;
        _analysisService = analysisService;
    }

    /// <summary>
    /// Create a follow-up argument (reply) to an existing argument
    /// </summary>
    public async Task<SocialArgument> CreateFollowUpArgumentAsync(
        Guid parentArgumentId,
        SocialArgument newArgument,
        string userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Validate parent argument exists and is accessible
        var parent = await db.SocialArguments
            .FirstOrDefaultAsync(a => a.Id == parentArgumentId, ct);
        
        if (parent == null)
            throw new ArgumentException($"Parent argument {parentArgumentId} not found");
        
        if (!parent.IsPublic && parent.UserId != userId)
            throw new UnauthorizedAccessException("Cannot reply to private argument");

        // Validate new argument
        await _validationService.ValidateSocialArgumentAsync(newArgument, ct);

        // Check rate limiting (5 replies per hour)
        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var recentReplies = await db.ArgumentLinks
            .CountAsync(l => 
                l.UserId == userId && 
                l.LinkType == LinkType.Reply &&
                l.CreatedAt >= hourAgo, ct);
        
        if (recentReplies >= 5)
            throw new InvalidOperationException("Rate limit exceeded: maximum 5 replies per hour");

        // Check maximum reply depth (5 levels)
        var depth = await GetReplyDepthAsync(parentArgumentId, db, ct);
        if (depth >= 5)
            throw new InvalidOperationException("Maximum reply depth (5 levels) reached");

        // Save the new argument
        newArgument.UserId = userId;
        newArgument.CreatedAt = DateTime.UtcNow;
        newArgument.UpdatedAt = DateTime.UtcNow;
        
        db.SocialArguments.Add(newArgument);
        await db.SaveChangesAsync(ct);

        // Create the reply link
        var replyLink = new ArgumentLink
        {
            SourceArgumentId = parentArgumentId,
            TargetArgumentId = newArgument.Id,
            LinkType = LinkType.Reply,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        db.ArgumentLinks.Add(replyLink);

        // Update parent's reply count
        parent.ReplyCount++;
        parent.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created follow-up argument {ReplyId} for parent {ParentId} by user {UserId}",
            newArgument.Id, parentArgumentId, userId);

        // ── Run the full Phase 1 analysis pipeline for this follow-up ──────
        // This creates a linked Argument record with claims, premises, evidence,
        // syllogisms, assumptions, qualifiers, rebuttals, and adjudication,
        // enabling the same "View Analysis" functionality as published arguments.
        try
        {
            await _analysisService.AnalyzeSocialArgumentAsync(newArgument.Id, ct);
            _logger.LogInformation(
                "Analysis pipeline completed for follow-up {ReplyId}",
                newArgument.Id);
        }
        catch (Exception ex)
        {
            // Don't fail the reply creation if analysis fails — the user can
            // retry analysis manually from the detail view.
            _logger.LogWarning(ex,
                "Analysis pipeline failed for follow-up {ReplyId}, user can retry manually",
                newArgument.Id);
        }

        return newArgument;
    }

    /// <summary>
    /// Get paginated follow-up arguments for a parent argument
    /// </summary>
    public async Task<(List<SocialArgument> Arguments, int TotalCount)> GetFollowUpArgumentsAsync(
        Guid parentArgumentId,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ArgumentLinks
            .Where(l => l.SourceArgumentId == parentArgumentId && l.LinkType == LinkType.Reply)
            .Join(db.SocialArguments,
                link => link.TargetArgumentId,
                arg => arg.Id,
                (link, arg) => arg)
            .Where(a => a.IsPublic || a.UserId == GetCurrentUserId())
            .OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var arguments = await query
            .Skip(skip)
            .Take(take)
            .Include(a => a.ClaimProposition)
            .Include(a => a.Votes.Where(v => v.UserId == GetCurrentUserId()))
            .ToListAsync(ct);

        return (arguments, totalCount);
    }

    /// <summary>
    /// Get nested replies for an argument (all levels)
    /// </summary>
    public async Task<Dictionary<Guid, List<SocialArgument>>> GetNestedRepliesAsync(
        Guid parentArgumentId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Get all reply links starting from the parent
        var allReplyLinks = await db.ArgumentLinks
            .Where(l => l.LinkType == LinkType.Reply)
            .ToListAsync(ct);

        // Build adjacency list
        var adjacencyList = allReplyLinks
            .GroupBy(l => l.SourceArgumentId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.TargetArgumentId).ToList());

        // Get all arguments involved
        var allArgumentIds = adjacencyList.Keys
            .Union(adjacencyList.Values.SelectMany(x => x))
            .Distinct()
            .ToList();

        var arguments = await db.SocialArguments
            .Where(a => allArgumentIds.Contains(a.Id))
            .Include(a => a.ClaimProposition)
            .Include(a => a.Votes.Where(v => v.UserId == GetCurrentUserId()))
            .ToDictionaryAsync(a => a.Id, ct);

        // Build nested structure
        var result = new Dictionary<Guid, List<SocialArgument>>();
        BuildNestedReplies(parentArgumentId, adjacencyList, arguments, result);

        return result;
    }

    /// <summary>
    /// Update reply counts for all arguments (background reconciliation)
    /// </summary>
    public async Task UpdateAllReplyCountsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Get all arguments with their reply counts
        var replyCounts = await db.ArgumentLinks
            .Where(l => l.LinkType == LinkType.Reply)
            .GroupBy(l => l.SourceArgumentId)
            .Select(g => new { ArgumentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Update each argument
        foreach (var count in replyCounts)
        {
            var argument = await db.SocialArguments.FindAsync(new object[] { count.ArgumentId }, ct);
            if (argument != null && argument.ReplyCount != count.Count)
            {
                argument.ReplyCount = count.Count;
                argument.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated reply counts for {Count} arguments", replyCounts.Count);
    }

    /// <summary>
    /// Get the reply depth for an argument
    /// </summary>
    private async Task<int> GetReplyDepthAsync(Guid argumentId, ApplicationDbContext db, CancellationToken ct)
    {
        var depth = 0;
        var currentId = argumentId;
        var visited = new HashSet<Guid>();

        while (true)
        {
            if (visited.Contains(currentId))
                break; // Cycle detected
            
            visited.Add(currentId);

            // Find parent of current argument (if any)
            var parentLink = await db.ArgumentLinks
                .FirstOrDefaultAsync(l => 
                    l.TargetArgumentId == currentId && 
                    l.LinkType == LinkType.Reply, ct);
            
            if (parentLink == null)
                break;
            
            depth++;
            currentId = parentLink.SourceArgumentId;

            if (depth >= 10) // Safety limit
                break;
        }

        return depth;
    }

    /// <summary>
    /// Recursively build nested replies structure
    /// </summary>
    private void BuildNestedReplies(
        Guid currentId,
        Dictionary<Guid, List<Guid>> adjacencyList,
        Dictionary<Guid, SocialArgument> arguments,
        Dictionary<Guid, List<SocialArgument>> result)
    {
        if (!adjacencyList.TryGetValue(currentId, out var childIds))
            return;

        var children = childIds
            .Where(id => arguments.ContainsKey(id))
            .Select(id => arguments[id])
            .ToList();

        result[currentId] = children;

        // Recursively build for each child
        foreach (var childId in childIds)
        {
            BuildNestedReplies(childId, adjacencyList, arguments, result);
        }
    }

    /// <summary>
    /// Get current user ID from the HTTP context
    /// </summary>
    private string GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?
            .FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }
}