using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Manages resolution endorsements — users endorsing resolutions (nexus points)
/// that reconcile contradictory positions. Used for the consensus_builder badge
/// and DMI Score computation.
/// </summary>
public class ResolutionEndorsementService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<ResolutionEndorsementService> _logger;

    public ResolutionEndorsementService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<ResolutionEndorsementService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Adds an endorsement from a user to a resolution.
    /// Returns true if the endorsement was newly created, false if already exists.
    /// </summary>
    public async Task<bool> AddEndorsementAsync(
        Guid resolutionId,
        string userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var exists = await db.ResolutionEndorsements
            .AnyAsync(e => e.ResolutionId == resolutionId && e.UserId == userId, ct);

        if (exists) return false;

        db.ResolutionEndorsements.Add(new ResolutionEndorsement
        {
            ResolutionId = resolutionId,
            UserId = userId
        });

        // Update denormalized counter on the resolution
        var resolution = await db.StructuralResolutions
            .FirstOrDefaultAsync(r => r.Id == resolutionId, ct);

        if (resolution is not null)
        {
            resolution.EndorsementCount++;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} endorsed resolution {ResolutionId}", userId, resolutionId);

        return true;
    }

    /// <summary>
    /// Removes a user's endorsement from a resolution.
    /// Returns true if the endorsement was removed, false if it didn't exist.
    /// </summary>
    public async Task<bool> RemoveEndorsementAsync(
        Guid resolutionId,
        string userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var endorsement = await db.ResolutionEndorsements
            .FirstOrDefaultAsync(e => e.ResolutionId == resolutionId && e.UserId == userId, ct);

        if (endorsement is null) return false;

        db.ResolutionEndorsements.Remove(endorsement);

        // Update denormalized counter
        var resolution = await db.StructuralResolutions
            .FirstOrDefaultAsync(r => r.Id == resolutionId, ct);

        if (resolution is not null && resolution.EndorsementCount > 0)
        {
            resolution.EndorsementCount--;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} removed endorsement from resolution {ResolutionId}",
            userId, resolutionId);

        return true;
    }

    /// <summary>
    /// Lists all users who endorsed a resolution.
    /// </summary>
    public async Task<List<string>> GetEndorsersAsync(
        Guid resolutionId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ResolutionEndorsements
            .AsNoTracking()
            .Where(e => e.ResolutionId == resolutionId)
            .Select(e => e.UserId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns the endorsement count for a resolution.
    /// </summary>
    public async Task<int> GetEndorsementCountAsync(
        Guid resolutionId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ResolutionEndorsements
            .CountAsync(e => e.ResolutionId == resolutionId, ct);
    }

    /// <summary>
    /// Checks if a user has endorsed a specific resolution.
    /// </summary>
    public async Task<bool> HasEndorsedAsync(
        Guid resolutionId,
        string userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ResolutionEndorsements
            .AnyAsync(e => e.ResolutionId == resolutionId && e.UserId == userId, ct);
    }
}