using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

public sealed class ClassificationDisputeService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ClassificationDisputeService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ClassificationDispute> CreateAsync(
        string userId,
        string targetType,
        string targetId,
        string classificationType,
        string rationale,
        string evidenceStatement,
        string? sourceUrl,
        CancellationToken ct = default)
    {
        var dispute = new ClassificationDispute
        {
            TargetType = targetType.Trim(),
            TargetId = targetId.Trim(),
            ClassificationType = classificationType.Trim(),
            RaisedByUserId = userId,
            Rationale = rationale.Trim()
        };
        dispute.Evidence.Add(new DisputeEvidence
        {
            SubmittedByUserId = userId,
            Statement = evidenceStatement.Trim(),
            SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim()
        });

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.ClassificationDisputes.Add(dispute);
        db.ClassificationAuditEntries.Add(new ClassificationAuditEntry
        {
            TargetType = dispute.TargetType,
            TargetId = dispute.TargetId,
            Action = "DisputeOpened",
            ChangedBy = userId,
            Reason = dispute.Rationale,
            NewValueJson = JsonSerializer.Serialize(new { dispute.Id, dispute.ClassificationType })
        });
        await db.SaveChangesAsync(ct);
        return dispute;
    }

    public async Task<IReadOnlyList<ClassificationAuditEntry>> GetAuditHistoryAsync(
        string targetType,
        string targetId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ClassificationAuditEntries.AsNoTracking()
            .Where(entry => entry.TargetType == targetType && entry.TargetId == targetId)
            .OrderByDescending(entry => entry.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ClassificationDispute?> GetAsync(Guid disputeId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ClassificationDisputes.AsNoTracking()
            .Include(dispute => dispute.Evidence)
            .SingleOrDefaultAsync(dispute => dispute.Id == disputeId, ct);
    }

    public async Task<bool> AddEvidenceAsync(
        Guid disputeId,
        string userId,
        string statement,
        string? sourceUrl,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var dispute = await db.ClassificationDisputes.SingleOrDefaultAsync(item => item.Id == disputeId, ct);
        if (dispute is null || dispute.Status is DisputeStatus.Resolved or DisputeStatus.Dismissed)
            return false;

        db.DisputeEvidence.Add(new DisputeEvidence
        {
            DisputeId = disputeId,
            SubmittedByUserId = userId,
            Statement = statement.Trim(),
            SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim()
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CanReviewAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Moderators.AsNoTracking()
            .AnyAsync(moderator => moderator.UserId == userId && moderator.IsActive, ct);
    }

    public async Task<bool> ResolveAsync(
        Guid disputeId,
        string reviewerUserId,
        DisputeResolution resolution,
        string notes,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var dispute = await db.ClassificationDisputes.SingleOrDefaultAsync(item => item.Id == disputeId, ct);
        if (dispute is null || dispute.Status is DisputeStatus.Resolved or DisputeStatus.Dismissed)
            return false;

        dispute.Status = resolution == DisputeResolution.InsufficientEvidence
            ? DisputeStatus.Dismissed
            : DisputeStatus.Resolved;
        dispute.Resolution = resolution;
        dispute.ResolutionNotes = notes.Trim();
        dispute.ReviewedByUserId = reviewerUserId;
        dispute.ResolvedAt = DateTime.UtcNow;

        db.ClassificationAuditEntries.Add(new ClassificationAuditEntry
        {
            TargetType = dispute.TargetType,
            TargetId = dispute.TargetId,
            Action = "DisputeResolved",
            ChangedBy = reviewerUserId,
            Reason = dispute.ResolutionNotes,
            NewValueJson = JsonSerializer.Serialize(new { resolution })
        });

        await db.SaveChangesAsync(ct);
        return true;
    }
}