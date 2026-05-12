using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Manages organizational stakeholders and their recorded positions on arguments.
/// </summary>
public class StakeholderService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<StakeholderService> _logger;

    public StakeholderService(ApplicationDbContext db, ILogger<StakeholderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Stakeholder identity
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds an existing stakeholder by case-insensitive name match, or creates a new one.
    /// </summary>
    public async Task<Stakeholder> RegisterOrGetAsync(string name, string? role = null, string? organization = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stakeholder name is required.", nameof(name));

        var normalized = name.Trim();
        var existing = await _db.Stakeholders
            .FirstOrDefaultAsync(s => s.Name.ToLower() == normalized.ToLower());

        if (existing != null)
        {
            // Update role/org if new details were provided
            if (!string.IsNullOrWhiteSpace(role))
                existing.Role = role.Trim();
            if (!string.IsNullOrWhiteSpace(organization))
                existing.Organization = organization.Trim();
            await _db.SaveChangesAsync();
            return existing;
        }

        var stakeholder = new Stakeholder
        {
            Name = normalized,
            Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
            Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim()
        };
        _db.Stakeholders.Add(stakeholder);
        await _db.SaveChangesAsync();
        return stakeholder;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Recording positions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Records a stakeholder's position on an argument.
    /// If the stakeholder already has a position on this argument, it is updated.
    /// </summary>
    public async Task<StakeholderPosition> RecordPositionAsync(
        int stakeholderId,
        int argumentId,
        StakeholderPositionType position,
        string? reasoning,
        IEnumerable<int>? acceptedPremiseIds = null,
        IEnumerable<int>? rejectedPremiseIds = null,
        bool isAnonymous = false)
    {
        var existing = await _db.StakeholderPositions
            .FirstOrDefaultAsync(p => p.StakeholderId == stakeholderId
                                   && p.ArgumentId == argumentId);

        if (existing != null)
        {
            existing.Position = position;
            existing.Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning.Trim();
            existing.AcceptedPremiseIdsJson =
                System.Text.Json.JsonSerializer.Serialize(acceptedPremiseIds?.ToList() ?? new List<int>());
            existing.RejectedPremiseIdsJson =
                System.Text.Json.JsonSerializer.Serialize(rejectedPremiseIds?.ToList() ?? new List<int>());
            existing.IsAnonymous = isAnonymous;
            await _db.SaveChangesAsync();
            return existing;
        }

        var sp = new StakeholderPosition
        {
            StakeholderId = stakeholderId,
            ArgumentId = argumentId,
            Position = position,
            Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning.Trim(),
            AcceptedPremiseIdsJson =
                System.Text.Json.JsonSerializer.Serialize(acceptedPremiseIds?.ToList() ?? new List<int>()),
            RejectedPremiseIdsJson =
                System.Text.Json.JsonSerializer.Serialize(rejectedPremiseIds?.ToList() ?? new List<int>()),
            IsAnonymous = isAnonymous
        };
        _db.StakeholderPositions.Add(sp);
        await _db.SaveChangesAsync();
        return sp;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Queries
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves all stakeholder positions for a given argument,
    /// including the related Stakeholder record.
    /// </summary>
    public async Task<List<StakeholderPosition>> GetPositionsForArgumentAsync(int argumentId)
    {
        return await _db.StakeholderPositions
            .Include(p => p.StakeholderRef)
            .Where(p => p.ArgumentId == argumentId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Computes a consensus summary for a given argument.
    /// </summary>
    public async Task<StakeholderConsensus> GetConsensusAsync(int argumentId)
    {
        var positions = await _db.StakeholderPositions
            .Where(p => p.ArgumentId == argumentId)
            .ToListAsync();

        if (!positions.Any())
            return new StakeholderConsensus();

        int supportCount   = positions.Count(p => p.Position == StakeholderPositionType.Support);
        int opposeCount    = positions.Count(p => p.Position == StakeholderPositionType.Oppose);
        int undecidedCount = positions.Count(p => p.Position == StakeholderPositionType.Undecided);
        int total = positions.Count;

        // Consensus rate: proportion taking the majority position
        double maxCount = Math.Max(supportCount, Math.Max(opposeCount, undecidedCount));
        double consensusRate = total > 0 ? maxCount / total : 0;

        return new StakeholderConsensus
        {
            TotalResponses = total,
            SupportCount   = supportCount,
            OpposeCount    = opposeCount,
            UndecidedCount = undecidedCount,
            ConsensusRate  = consensusRate
        };
    }
}

/// <summary>DTO summarizing stakeholder consensus for an argument.</summary>
public record StakeholderConsensus
{
    public int TotalResponses  { get; init; }
    public int SupportCount    { get; init; }
    public int OpposeCount     { get; init; }
    public int UndecidedCount  { get; init; }
    /// <summary>Proportion [0–1] of stakeholders holding the majority position.</summary>
    public double ConsensusRate { get; init; }
    public bool HasConsensus => ConsensusRate >= 0.66;
    public string MajorityPosition => SupportCount >= OpposeCount && SupportCount >= UndecidedCount
                                        ? "Support"
                                        : OpposeCount >= SupportCount && OpposeCount >= UndecidedCount
                                            ? "Oppose"
                                            : "Undecided";
}
