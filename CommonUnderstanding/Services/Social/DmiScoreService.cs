using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Computes the Dialectical Mastery Index (DMI) for users.
/// DMI is a composite score that measures peacemaking behavior:
/// (ResolutionCount × 2.0) + (AlignmentMatricesCreated × 1.5) + (ChangedMindCount × 3.0)
/// + (CrossAisleUpvotes × 0.5) + (ResolutionsEndorsedByOthers × 1.0)
/// </summary>
public class DmiScoreService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<DmiScoreService> _logger;

    public DmiScoreService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<DmiScoreService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Computes and persists the DMI score for a single user.
    /// </summary>
    public async Task<double> ComputeForUserAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var resolutionCount = await db.StructuralResolutions
            .CountAsync(r => r.AuthorId == userId, ct);

        var changedMindCount = await db.ArgumentVotes
            .CountAsync(v => v.Argument.UserId == userId
                          && v.Rationale == VoteRationale.ChangedMyView, ct);

        var crossAisleUpvotes = await db.ArgumentVotes
            .CountAsync(v => v.UserId == userId && v.Vote == VoteValue.Up, ct);

        var endorsementsReceived = await db.ResolutionEndorsements
            .CountAsync(e => e.UserId == userId, ct);

        // Alignment matrices: count ConvergenceMaps where user is User1Id or User2Id
        var alignmentMatricesCreated = await db.ConvergenceMaps
            .CountAsync(m => m.User1Id == userId || m.User2Id == userId, ct);

        var dmi = (resolutionCount * 2.0)
                + (alignmentMatricesCreated * 1.5)
                + (changedMindCount * 3.0)
                + (crossAisleUpvotes * 0.5)
                + (endorsementsReceived * 1.0);

        // Persist to UserReputation
        var rep = await db.UserReputations
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is not null)
        {
            rep.DmiScore = dmi;
            await db.SaveChangesAsync(ct);
        }

        return dmi;
    }

    /// <summary>
    /// Recomputes DMI scores for all users. Called by DmiScoreWorker on a schedule.
    /// </summary>
    public async Task RecomputeAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var userIds = await db.UserReputations
            .Select(r => r.UserId)
            .ToListAsync(ct);

        _logger.LogInformation("Recomputing DMI scores for {Count} users", userIds.Count);

        foreach (var userId in userIds)
        {
            if (ct.IsCancellationRequested) break;
            await ComputeForUserAsync(userId, ct);
        }

        _logger.LogInformation("DMI score recomputation complete");
    }
}