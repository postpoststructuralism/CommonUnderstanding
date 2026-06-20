using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social.Plugins;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Business logic for Worldview CRUD, chain management, Schwartz vector computation,
/// embedding centroid updates, and convergence scoring.
/// </summary>
public class WorldviewService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly WorldviewConvergencePlugin _convergencePlugin;
    private readonly XPAwardService _xpAwards;
    private readonly ILogger<WorldviewService> _logger;

    public WorldviewService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        WorldviewConvergencePlugin convergencePlugin,
        XPAwardService xpAwards,
        ILogger<WorldviewService> logger)
    {
        _dbFactory = dbFactory;
        _convergencePlugin = convergencePlugin;
        _xpAwards = xpAwards;
        _logger = logger;
    }

    // ── Worldview CRUD ────────────────────────────────────────────────────────

    public async Task<Worldview> CreateWorldviewAsync(
        string userId,
        string title,
        string? description,
        string[] tags,
        bool isPublic,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var worldview = new Worldview
        {
            Title = title,
            Description = description,
            UserId = userId,
            IsPublic = isPublic,
            Tags = tags
        };

        db.Worldviews.Add(worldview);
        await db.SaveChangesAsync(ct);

        return worldview;
    }

    public async Task<Worldview?> GetWorldviewAsync(Guid worldviewId, string? requestingUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var wv = await db.Worldviews
            .AsNoTracking()
            .Include(w => w.WorldviewChains)
                .ThenInclude(wc => wc.ArgumentChain)
            .FirstOrDefaultAsync(w => w.Id == worldviewId, ct);

        if (wv is null) return null;
        if (!wv.IsPublic && wv.UserId != requestingUserId) return null;

        return wv;
    }

    // ── Chain management ──────────────────────────────────────────────────────

    public async Task<bool> AddChainAsync(
        Guid worldviewId,
        Guid chainId,
        string requestingUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var worldview = await db.Worldviews
            .Include(w => w.WorldviewChains)
            .FirstOrDefaultAsync(w => w.Id == worldviewId && w.UserId == requestingUserId, ct);

        if (worldview is null) return false;

        // Validate chain exists and is accessible
        var chain = await db.ArgumentChains
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chainId && (c.IsPublic || c.UserId == requestingUserId), ct);

        if (chain is null) return false;

        if (worldview.WorldviewChains.Any(wc => wc.ArgumentChainId == chainId)) return true;

        worldview.WorldviewChains.Add(new WorldviewChain
        {
            WorldviewId = worldviewId,
            ArgumentChainId = chainId,
            OrderIndex = worldview.WorldviewChains.Count
        });

        worldview.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Recompute Schwartz vector and embedding centroid synchronously (cheap operation)
        await UpdateWorldviewMetadataAsync(worldviewId, db, ct);

        // Award XP when a worldview reaches 2+ chains and is published
        if (worldview.IsPublic && worldview.WorldviewChains.Count >= 2)
            await _xpAwards.AwardAsync(requestingUserId, 30, "Published Worldview with ≥2 chains", worldviewId, ct);

        return true;
    }

    public async Task<bool> RemoveChainAsync(
        Guid worldviewId,
        Guid chainId,
        string requestingUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var link = await db.WorldviewChains
            .FirstOrDefaultAsync(wc =>
                wc.WorldviewId == worldviewId &&
                wc.ArgumentChainId == chainId &&
                wc.Worldview.UserId == requestingUserId, ct);

        if (link is null) return false;

        db.WorldviewChains.Remove(link);

        var worldview = await db.Worldviews.FindAsync(new object[] { worldviewId }, ct);
        if (worldview is not null) worldview.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await UpdateWorldviewMetadataAsync(worldviewId, db, ct);

        return true;
    }

    public async Task<bool> ReorderChainsAsync(
        Guid worldviewId,
        Guid[] orderedChainIds,
        string requestingUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var worldview = await db.Worldviews
            .Include(w => w.WorldviewChains)
            .FirstOrDefaultAsync(w => w.Id == worldviewId && w.UserId == requestingUserId, ct);

        if (worldview is null) return false;

        for (int i = 0; i < orderedChainIds.Length; i++)
        {
            var wc = worldview.WorldviewChains.FirstOrDefault(c => c.ArgumentChainId == orderedChainIds[i]);
            if (wc is not null) wc.OrderIndex = i;
        }

        worldview.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return true;
    }

    // ── Convergence ───────────────────────────────────────────────────────────

    public async Task<ConvergenceResult> ComputeConvergenceAsync(
        Guid worldviewAId,
        Guid worldviewBId,
        bool includeNarrative = false,
        CancellationToken ct = default)
    {
        return await _convergencePlugin.ComputeConvergenceAsync(
            worldviewAId, worldviewBId, includeNarrative, ct);
    }

    // ── Metadata recomputation ────────────────────────────────────────────────

    /// <summary>
    /// Recomputes the Schwartz vector and embedding centroid for a worldview.
    /// Called synchronously on chain add/remove — centroid is a pure vector average, no API call needed.
    /// </summary>
    private async Task UpdateWorldviewMetadataAsync(
        Guid worldviewId,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var worldview = await db.Worldviews
            .Include(w => w.WorldviewChains)
                .ThenInclude(wc => wc.ArgumentChain)
            .FirstOrDefaultAsync(w => w.Id == worldviewId, ct);

        if (worldview is null) return;

        var allArgIds = worldview.WorldviewChains
            .SelectMany(wc => wc.ArgumentChain?.ArgumentIds ?? Array.Empty<Guid>())
            .Distinct()
            .ToList();

        if (allArgIds.Count == 0) return;

        // Fetch argument metadata for Schwartz vector computation
        var args = await db.SocialArguments
            .AsNoTracking()
            .Where(a => allArgIds.Contains(a.Id))
            .ToListAsync(ct);

        // Aggregate Schwartz values (union)
        worldview.SchwartzValues = args
            .SelectMany(a => a.SchwartzValues)
            .Distinct()
            .ToArray();

        // Compute 10-dimensional Schwartz vector (frequency-weighted)
        worldview.SchwartzVector = ComputeSchwartzVector(args);

        // Compute embedding centroid
        var embeddings = args.Select(a => a.Embedding).ToList();
        var centroid = ScoringAlgorithms.ComputeCentroid(embeddings);
        if (centroid is not null)
            worldview.Embedding = centroid;

        worldview.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static double[] ComputeSchwartzVector(List<SocialArgument> args)
    {
        var dims = WorldviewConvergencePlugin.SchwartzDimensions;
        var vector = new double[dims.Length];

        if (args.Count == 0) return vector;

        for (int i = 0; i < dims.Length; i++)
        {
            int count = args.Count(a =>
                a.SchwartzValues.Contains(dims[i], StringComparer.OrdinalIgnoreCase));
            vector[i] = (double)count / args.Count;
        }

        return vector;
    }
}
