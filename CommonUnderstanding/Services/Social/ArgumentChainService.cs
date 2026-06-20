using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Business logic for ArgumentChain CRUD, DAG cycle detection, and graph traversal.
/// The cycle detection uses BFS from the target to check if the source is reachable
/// before any new ArgumentLink is committed.
/// </summary>
public class ArgumentChainService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly EmbeddingService _embeddingService;
    private readonly XPAwardService _xpAwards;
    private readonly ILogger<ArgumentChainService> _logger;

    public ArgumentChainService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        EmbeddingService embeddingService,
        XPAwardService xpAwards,
        ILogger<ArgumentChainService> logger)
    {
        _dbFactory = dbFactory;
        _embeddingService = embeddingService;
        _xpAwards = xpAwards;
        _logger = logger;
    }

    // ── Argument Chain CRUD ───────────────────────────────────────────────────

    public async Task<ArgumentChain> CreateChainAsync(
        string userId,
        string title,
        string? description,
        Guid rootArgumentId,
        Guid[] argumentIds,
        string[] tags,
        bool isPublic,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chain = new ArgumentChain
        {
            Title = title,
            Description = description,
            RootArgumentId = rootArgumentId,
            ArgumentIds = argumentIds,
            Tags = tags,
            IsPublic = isPublic,
            UserId = userId
        };

        db.ArgumentChains.Add(chain);
        await db.SaveChangesAsync(ct);

        // Award XP for chains with at least 3 arguments
        if (argumentIds.Length >= 3)
            await _xpAwards.AwardAsync(userId, 20, "Created ArgumentChain with ≥3 arguments", chain.Id, ct);

        // Compute and persist embedding centroid asynchronously
        _ = UpdateChainEmbeddingAsync(chain.Id, argumentIds);

        return chain;
    }

    public async Task<ArgumentChain?> GetChainAsync(Guid chainId, string? requestingUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chain = await db.ArgumentChains
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chainId, ct);

        if (chain is null) return null;
        if (!chain.IsPublic && chain.UserId != requestingUserId) return null;

        return chain;
    }

    public async Task<bool> AddArgumentToChainAsync(
        Guid chainId,
        Guid argumentId,
        string requestingUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chain = await db.ArgumentChains
            .FirstOrDefaultAsync(c => c.Id == chainId && c.UserId == requestingUserId, ct);

        if (chain is null) return false;

        if (chain.ArgumentIds.Contains(argumentId)) return true; // Already present

        chain.ArgumentIds = chain.ArgumentIds.Append(argumentId).ToArray();
        chain.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _ = UpdateChainEmbeddingAsync(chainId, chain.ArgumentIds);

        return true;
    }

    public async Task<bool> RemoveArgumentFromChainAsync(
        Guid chainId,
        Guid argumentId,
        string requestingUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chain = await db.ArgumentChains
            .FirstOrDefaultAsync(c => c.Id == chainId && c.UserId == requestingUserId, ct);

        if (chain is null) return false;

        chain.ArgumentIds = chain.ArgumentIds.Where(id => id != argumentId).ToArray();
        chain.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return true;
    }

    // ── Argument Link creation with cycle detection ───────────────────────────

    /// <summary>
    /// Creates a new ArgumentLink if it does not introduce a cycle.
    /// Uses BFS from TargetArgumentId to check if SourceArgumentId is reachable.
    /// Returns null with a rejection reason if the link would create a cycle or self-loop.
    /// </summary>
    public async Task<(ArgumentLink? Link, string? Error)> CreateLinkAsync(
        Guid sourceArgumentId,
        Guid targetArgumentId,
        LinkType linkType,
        string? annotation,
        string userId,
        CancellationToken ct = default)
    {
        if (sourceArgumentId == targetArgumentId)
            return (null, "An argument cannot link to itself.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Check both arguments exist and are accessible
        var sourceExists = await db.SocialArguments.AnyAsync(a => a.Id == sourceArgumentId, ct);
        var targetExists = await db.SocialArguments.AnyAsync(a => a.Id == targetArgumentId, ct);

        if (!sourceExists) return (null, "Source argument not found.");
        if (!targetExists) return (null, "Target argument not found.");

        // BFS cycle detection: is sourceArgumentId reachable from targetArgumentId?
        if (await WouldCreateCycleAsync(db, sourceArgumentId, targetArgumentId, ct))
            return (null, "This link would create a cycle in the argument graph.");

        var link = new ArgumentLink
        {
            SourceArgumentId = sourceArgumentId,
            TargetArgumentId = targetArgumentId,
            LinkType = linkType,
            Annotation = annotation,
            UserId = userId
        };

        db.ArgumentLinks.Add(link);
        await db.SaveChangesAsync(ct);

        return (link, null);
    }

    /// <summary>
    /// Returns the full argument graph reachable from a root argument (up to maxDepth hops).
    /// </summary>
    public async Task<ArgumentGraphDto> GetArgumentGraphAsync(
        Guid rootArgumentId,
        int maxDepth = 2,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var visited = new HashSet<Guid>();
        var nodes = new List<ArgumentNodeDto>();
        var edges = new List<ArgumentEdgeDto>();

        await BfsGraphAsync(db, rootArgumentId, maxDepth, visited, nodes, edges, ct);

        return new ArgumentGraphDto(nodes, edges);
    }

    // ── Cycle detection (BFS) ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true if adding edge (source → target) would create a cycle.
    /// Cycle exists if source is reachable from target via existing links.
    /// </summary>
    private static async Task<bool> WouldCreateCycleAsync(
        ApplicationDbContext db,
        Guid sourceId,
        Guid targetId,
        CancellationToken ct)
    {
        // BFS from targetId following outbound links; returns true if sourceId is found
        var queue = new Queue<Guid>();
        var seen = new HashSet<Guid>();

        queue.Enqueue(targetId);
        seen.Add(targetId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == sourceId) return true;

            var neighbors = await db.ArgumentLinks
                .AsNoTracking()
                .Where(l => l.SourceArgumentId == current)
                .Select(l => l.TargetArgumentId)
                .ToListAsync(ct);

            foreach (var neighbor in neighbors.Where(n => seen.Add(n)))
                queue.Enqueue(neighbor);
        }

        return false;
    }

    // ── Graph BFS traversal ───────────────────────────────────────────────────

    private static async Task BfsGraphAsync(
        ApplicationDbContext db,
        Guid nodeId,
        int depth,
        HashSet<Guid> visited,
        List<ArgumentNodeDto> nodes,
        List<ArgumentEdgeDto> edges,
        CancellationToken ct)
    {
        if (depth < 0 || !visited.Add(nodeId)) return;

        var arg = await db.SocialArguments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == nodeId, ct);

        if (arg is null) return;

        nodes.Add(new ArgumentNodeDto(arg.Id, arg.Title, arg.WilsonScore, arg.UserId));

        var links = await db.ArgumentLinks
            .AsNoTracking()
            .Where(l => l.SourceArgumentId == nodeId)
            .ToListAsync(ct);

        foreach (var link in links)
        {
            edges.Add(new ArgumentEdgeDto(link.Id, link.SourceArgumentId, link.TargetArgumentId, link.LinkType.ToString()));
            await BfsGraphAsync(db, link.TargetArgumentId, depth - 1, visited, nodes, edges, ct);
        }
    }

    // ── Embedding centroid update ─────────────────────────────────────────────

    private async Task UpdateChainEmbeddingAsync(Guid chainId, Guid[] argumentIds)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(CancellationToken.None);

            var embeddings = await db.SocialArguments
                .AsNoTracking()
                .Where(a => argumentIds.Contains(a.Id) && a.Embedding != null)
                .Select(a => a.Embedding)
                .ToListAsync(CancellationToken.None);

            var centroid = ScoringAlgorithms.ComputeCentroid(embeddings);
            if (centroid is null) return;

            var chain = await db.ArgumentChains.FindAsync(chainId);
            if (chain is not null)
            {
                chain.Embedding = centroid;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update chain embedding for {ChainId}.", chainId);
        }
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ArgumentGraphDto(List<ArgumentNodeDto> Nodes, List<ArgumentEdgeDto> Edges);
public record ArgumentNodeDto(Guid Id, string Title, double Score, string UserId);
public record ArgumentEdgeDto(Guid Id, Guid Source, Guid Target, string LinkType);
