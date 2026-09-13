using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

public sealed record ReferenceFrameworkMatch(
    Guid ReferencePropositionId,
    string Text,
    ReferenceRelationshipType RelationshipType,
    double Similarity);

public sealed record ReferenceFrameworkFitResult(
    Guid FrameworkId,
    string FrameworkName,
    double FitScore,
    double TensionScore,
    int SupportCount,
    int TensionCount,
    int UnclassifiedCount,
    IReadOnlyList<ReferenceFrameworkMatch> StrongestMatches);

public interface IReferenceFrameworkFitService
{
    Task<IReadOnlyList<ReferenceFramework>> GetAvailableFrameworksAsync(string? userId, CancellationToken cancellationToken = default);
    Task<ReferenceFrameworkFitResult> ScorePropositionAsync(int propositionId, Guid frameworkId, string? userId, CancellationToken cancellationToken = default);
    Task<ReferenceFrameworkFitResult> ScoreSocialArgumentAsync(Guid socialArgumentId, Guid frameworkId, string? userId, CancellationToken cancellationToken = default);
}

public class ReferenceFrameworkFitService : IReferenceFrameworkFitService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly EmbeddingService _embeddingService;

    public ReferenceFrameworkFitService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        EmbeddingService embeddingService)
    {
        _dbFactory = dbFactory;
        _embeddingService = embeddingService;
    }

    public async Task<IReadOnlyList<ReferenceFramework>> GetAvailableFrameworksAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await AvailableFrameworks(db, userId)
            .AsNoTracking()
            .Include(framework => framework.Owners)
            .Include(framework => framework.Propositions)
            .OrderBy(framework => framework.Name)
            .ThenByDescending(framework => framework.ImportedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReferenceFrameworkFitResult> ScorePropositionAsync(
        int propositionId,
        Guid frameworkId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var proposition = await db.Propositions.FindAsync([propositionId], cancellationToken)
            ?? throw new KeyNotFoundException("Proposition not found.");
        var embedding = await _embeddingService.GenerateEmbeddingAsync(proposition.Text, cancellationToken);
        return await ScoreAsync(db, frameworkId, userId, embedding, proposition.Status, propositionId, null, cancellationToken);
    }

    public async Task<ReferenceFrameworkFitResult> ScoreSocialArgumentAsync(
        Guid socialArgumentId,
        Guid frameworkId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var argument = await db.SocialArguments
            .Include(item => item.ClaimProposition)
            .FirstOrDefaultAsync(item => item.Id == socialArgumentId, cancellationToken)
            ?? throw new KeyNotFoundException("Social argument not found.");
        var text = argument.ClaimProposition?.Text ?? argument.Title;
        var embedding = argument.Embedding ?? await _embeddingService.GenerateEmbeddingAsync(text, cancellationToken);
        return await ScoreAsync(db, frameworkId, userId, embedding, PropositionStatus.Unevaluated, null, socialArgumentId, cancellationToken);
    }

    private static IQueryable<ReferenceFramework> AvailableFrameworks(ApplicationDbContext db, string? userId) =>
        db.ReferenceFrameworks.Where(framework =>
            framework.IsShared || (userId != null && framework.Owners.Any(owner => owner.UserId == userId)));

    private static async Task<ReferenceFrameworkFitResult> ScoreAsync(
        ApplicationDbContext db,
        Guid frameworkId,
        string? userId,
        float[]? targetEmbedding,
        PropositionStatus targetStatus,
        int? propositionId,
        Guid? socialArgumentId,
        CancellationToken cancellationToken)
    {
        var framework = await AvailableFrameworks(db, userId)
            .Include(item => item.Propositions)
            .FirstOrDefaultAsync(item => item.Id == frameworkId, cancellationToken)
            ?? throw new KeyNotFoundException("Reference framework not found or not accessible.");

        var matches = framework.Propositions.Select(reference =>
        {
            var similarity = targetEmbedding != null && reference.Embedding != null
                ? ReferenceRelationshipClassifier.CosineSimilarity(targetEmbedding, reference.Embedding)
                : 0;
            var graphRelationship = ReferenceRelationshipClassifier.Classify(
                similarity,
                targetStatus,
                PropositionStatus.Settled,
                sharesContext: true);
            var relationshipType = graphRelationship switch
            {
                "supports" => ReferenceRelationshipType.Supports,
                "contradicts" => ReferenceRelationshipType.Tensions,
                _ => ReferenceRelationshipType.Unclassified
            };
            return new ReferenceFrameworkMatch(reference.Id, reference.Text, relationshipType, similarity);
        }).ToList();

        var relationships = matches.Select(match => new ReferenceFrameworkRelationship
        {
            ReferencePropositionId = match.ReferencePropositionId,
            PropositionId = propositionId,
            SocialArgumentId = socialArgumentId,
            RelationshipType = match.RelationshipType,
            Score = match.Similarity
        }).ToList();
        var referenceIds = relationships.Select(item => item.ReferencePropositionId).ToList();
        var existing = await db.ReferenceFrameworkRelationships
            .Where(item => referenceIds.Contains(item.ReferencePropositionId) &&
                item.PropositionId == propositionId && item.SocialArgumentId == socialArgumentId)
            .ToListAsync(cancellationToken);
        db.ReferenceFrameworkRelationships.RemoveRange(existing);
        db.ReferenceFrameworkRelationships.AddRange(relationships);
        await db.SaveChangesAsync(cancellationToken);

        var support = matches.Where(item => item.RelationshipType == ReferenceRelationshipType.Supports).ToList();
        var tension = matches.Where(item => item.RelationshipType == ReferenceRelationshipType.Tensions).ToList();
        return new ReferenceFrameworkFitResult(
            framework.Id,
            framework.Name,
            support.Count == 0 ? 0 : support.Average(item => item.Similarity),
            tension.Count == 0 ? 0 : tension.Average(item => 1 - item.Similarity),
            support.Count,
            tension.Count,
            matches.Count - support.Count - tension.Count,
            matches.OrderByDescending(item => item.RelationshipType != ReferenceRelationshipType.Unclassified)
                .ThenByDescending(item => item.RelationshipType == ReferenceRelationshipType.Tensions ? 1 - item.Similarity : item.Similarity)
                .Take(10)
                .ToList());
    }
}