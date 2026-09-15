using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace CommonUnderstanding.Services.Provenance;

public interface IEvidenceMatchingService
{
    Task<int> SuggestForPropositionAsync(int propositionId, CancellationToken cancellationToken = default);
    Task<int?> ReviewAsync(long suggestionId, bool confirm, string? reviewedBy, CancellationToken cancellationToken = default);
}

public sealed class EvidenceMatchingService : IEvidenceMatchingService
{
    private const int RetrievalLimit = 3;
    private static readonly TimeSpan ClassificationTimeout = TimeSpan.FromSeconds(20);

    private readonly ApplicationDbContext _db;
    private readonly EmbeddingService _embeddingService;
    private readonly SemanticKernelService _kernelService;
    private readonly AdjudicationEngine _adjudicationEngine;
    private readonly ILogger<EvidenceMatchingService> _logger;

    public EvidenceMatchingService(
        ApplicationDbContext db,
        EmbeddingService embeddingService,
        SemanticKernelService kernelService,
        AdjudicationEngine adjudicationEngine,
        ILogger<EvidenceMatchingService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _kernelService = kernelService;
        _adjudicationEngine = adjudicationEngine;
        _logger = logger;
    }

    public async Task<int> SuggestForPropositionAsync(int propositionId, CancellationToken cancellationToken = default)
    {
        var proposition = await _db.Propositions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propositionId, cancellationToken);
        if (proposition is null)
        {
            _logger.LogWarning("Proposition {PropositionId} not found.", propositionId);
            return 0;
        }

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(proposition.Text, cancellationToken);
        if (queryEmbedding is null)
        {
            _logger.LogWarning("Embedding generation returned null for proposition {PropositionId}.", propositionId);
            return 0;
        }
        var existingEntryIds = await _db.EvidenceMatchSuggestions
            .AsNoTracking()
            .Where(match => match.PropositionId == propositionId)
            .Select(match => match.EvidenceCorpusEntryId)
            .ToHashSetAsync(cancellationToken);
        var pendingCount = await _db.EvidenceMatchSuggestions
            .AsNoTracking()
            .CountAsync(
                match => match.PropositionId == propositionId && match.Status == EvidenceMatchStatus.Pending,
                cancellationToken);
        var availableSuggestionSlots = Math.Max(0, RetrievalLimit - pendingCount);
        if (availableSuggestionSlots == 0) return 0;

        var corpus = await _db.EvidenceCorpusEntries
            .AsNoTracking()
            .Include(entry => entry.Source)
            .Where(entry => entry.Embedding != null
                && entry.VerificationStatus != SourceVerificationStatus.Retracted
                && !existingEntryIds.Contains(entry.Id))
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var candidates = SelectCandidates(corpus, queryEmbedding, availableSuggestionSlots);
        if (candidates.Count == 0) return 0;

        var classified = await ClassifyAsync(proposition.Text, candidates, cancellationToken);

        var added = 0;
        foreach (var result in classified)
        {
            var candidate = candidates.Single(item => item.Entry.Id == result.CorpusEntryId);
            _db.EvidenceMatchSuggestions.Add(new EvidenceMatchSuggestion
            {
                PropositionId = propositionId,
                EvidenceCorpusEntryId = result.CorpusEntryId,
                Direction = result.Direction,
                Status = EvidenceMatchStatus.Pending,
                SimilarityScore = candidate.Similarity,
                ClassificationConfidence = result.Confidence,
                Rationale = result.Rationale,
                SuggestedTier = result.Tier
            });
            added++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return added;
    }

    internal static List<Candidate> SelectCandidates(
        IEnumerable<EvidenceCorpusEntry> corpus,
        float[] queryEmbedding,
        int limit = RetrievalLimit)
    {
        return corpus
            .Select(entry => new Candidate(entry, ReferenceRelationshipClassifier.CosineSimilarity(queryEmbedding, entry.Embedding!)))
            .Where(candidate => candidate.Similarity >= 0.2)
            .OrderByDescending(candidate => candidate.Similarity)
            .Take(limit)
            .ToList();
    }

    public async Task<int?> ReviewAsync(
        long suggestionId,
        bool confirm,
        string? reviewedBy,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await _db.EvidenceMatchSuggestions
            .Include(item => item.Proposition)
                .ThenInclude(item => item!.Claim)
            .Include(item => item.EvidenceCorpusEntry)
                .ThenInclude(item => item!.Source)
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);

        if (suggestion is null || suggestion.Status != EvidenceMatchStatus.Pending)
            return null;

        suggestion.Status = confirm ? EvidenceMatchStatus.Confirmed : EvidenceMatchStatus.Rejected;
        suggestion.ReviewedAt = DateTime.UtcNow;
        suggestion.ReviewedBy = reviewedBy;

        if (confirm && suggestion.Direction != EvidenceMatchDirection.Irrelevant)
        {
            var entry = suggestion.EvidenceCorpusEntry!;
            var evidence = new EvidenceItem
            {
                PropositionId = suggestion.PropositionId,
                SourceId = entry.SourceId,
                EvidenceCorpusEntryId = entry.Id,
                Citation = entry.Title,
                SourceUri = entry.Uri,
                DOI = entry.DOI,
                Tier = suggestion.SuggestedTier,
                Direction = suggestion.Direction == EvidenceMatchDirection.Supports
                    ? EvidenceDirection.Supports
                    : EvidenceDirection.Opposes,
                PublicationYear = entry.PublicationYear,
                AddedBy = reviewedBy
            };
            _db.EvidenceItems.Add(evidence);
            await _db.SaveChangesAsync(cancellationToken);
            suggestion.EvidenceItemId = evidence.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var argumentId = suggestion.Proposition?.Claim?.ArgumentId;
        if (confirm && argumentId.HasValue)
            await _adjudicationEngine.AdjudicateAsync(argumentId.Value, cancellationToken: cancellationToken);

        return argumentId;
    }

    private async Task<List<ClassificationResult>> ClassifyAsync(
        string propositionText,
        List<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        var candidatePayload = candidates.Select(candidate => new
        {
            id = candidate.Entry.Id,
            title = candidate.Entry.Title,
            abstractText = candidate.Entry.Abstract,
            keyFindings = candidate.Entry.KeyFindings,
            source = candidate.Entry.Source?.Name,
            verification = candidate.Entry.VerificationStatus.ToString()
        });

        var prompt = $$$"""
        Always respond in English. Classify only the retrieved evidence entries below.
        Never invent an entry, identifier, citation, author, finding, or source.

        PROPOSITION:
        {{{propositionText}}}

        RETRIEVED ENTRIES:
        {{{JsonSerializer.Serialize(candidatePayload)}}}

        Return JSON only in this shape:
        {"matches":[{"id":1,"direction":"Supports|Contradicts|Irrelevant","tier":"T1_SystematicReview|T2_RCT|T3_Observational|T4_ExpertConsensus|T5_CaseStudy|T6_AnecdoteOpinion","confidence":0.0,"rationale":"one sentence grounded only in the supplied title, abstract, or key findings"}]}
        Include every retrieved ID exactly once. Mark uncertain relationships Irrelevant.
        """;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ClassificationTimeout);
            var response = await _kernelService.GetKernel().InvokePromptAsync(prompt, cancellationToken: timeout.Token);
            return ParseClassifications(response.ToString(), candidates.Select(item => item.Entry.Id).ToHashSet());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Evidence match classification failed for proposition text of length {Length}.", propositionText.Length);
            return [];
        }
    }

    internal static List<ClassificationResult> ParseClassifications(string content, HashSet<long> allowedIds)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start) return [];

        try
        {
            using var document = JsonDocument.Parse(content[start..(end + 1)]);
            if (!document.RootElement.TryGetProperty("matches", out var matches)) return [];

            var results = new List<ClassificationResult>();
            foreach (var match in matches.EnumerateArray())
            {
                if (!match.TryGetProperty("id", out var idElement)
                    || !idElement.TryGetInt64(out var id)
                    || !allowedIds.Contains(id)
                    || results.Any(existing => existing.CorpusEntryId == id))
                    continue;

                if (!Enum.TryParse<EvidenceMatchDirection>(match.GetProperty("direction").GetString(), true, out var direction))
                    direction = EvidenceMatchDirection.Irrelevant;
                if (!Enum.TryParse<EvidenceTier>(match.GetProperty("tier").GetString(), true, out var tier))
                    tier = EvidenceTier.T6_AnecdoteOpinion;

                var rationale = match.TryGetProperty("rationale", out var rationaleElement)
                    ? rationaleElement.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(rationale)) continue;

                var confidence = match.TryGetProperty("confidence", out var confidenceElement)
                    && confidenceElement.TryGetDouble(out var parsedConfidence)
                    ? Math.Clamp(parsedConfidence, 0, 1)
                    : 0;

                results.Add(new ClassificationResult(id, direction, tier, confidence, rationale[..Math.Min(500, rationale.Length)]));
            }

            return results;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal sealed record Candidate(EvidenceCorpusEntry Entry, double Similarity);
    internal sealed record ClassificationResult(
        long CorpusEntryId,
        EvidenceMatchDirection Direction,
        EvidenceTier Tier,
        double Confidence,
        string Rationale);
}