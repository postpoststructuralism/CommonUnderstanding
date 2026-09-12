using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

public class WorldviewInsightService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly UserProfileStore _profileStore;
    private readonly BeliefSystemKnowledgeBase _knowledgeBase;
    private readonly WorldviewSummaryService _summaryService;
    private readonly EmbeddingService _embeddingService;

    public WorldviewInsightService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        UserProfileStore profileStore,
        BeliefSystemKnowledgeBase knowledgeBase,
        WorldviewSummaryService summaryService,
        EmbeddingService embeddingService)
    {
        _dbFactory = dbFactory;
        _profileStore = profileStore;
        _knowledgeBase = knowledgeBase;
        _summaryService = summaryService;
        _embeddingService = embeddingService;
    }

    public async Task<WorldviewInsight> BuildAsync(string userId, CancellationToken ct = default)
    {
        var profile = _profileStore.GetProfile(userId);
        var snapshot = profile?.CurrentBeliefSnapshot;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var primaryArguments = await db.Arguments
            .AsNoTracking()
            .Include(argument => argument.Claims)
            .Where(argument => argument.SubmittedBy == userId)
            .OrderByDescending(argument => argument.UpdatedAt ?? argument.CreatedAt)
            .Take(12)
            .Select(argument => new WorldviewInsightArgument(
                argument.Id.ToString(),
                "Argument",
                "View",
                argument.Title,
                argument.Claims.Select(claim => claim.Text).FirstOrDefault() ?? argument.RawText,
                argument.Claims
                    .Where(claim => claim.ClaimType != null)
                    .Select(claim => claim.ClaimType!)
                    .Distinct()
                    .ToArray(),
                argument.UpdatedAt ?? argument.CreatedAt))
            .ToListAsync(ct);

        var publishedSourceIds = primaryArguments
            .Select(argument => int.TryParse(argument.Id, out var id) ? (int?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        var socialArguments = await db.SocialArguments
            .AsNoTracking()
            .Include(argument => argument.ClaimProposition)
            .Where(argument => argument.UserId == userId
                && !argument.IsShadowBanned
                && (argument.SourceArgumentId == null || !publishedSourceIds.Contains(argument.SourceArgumentId.Value)))
            .OrderByDescending(argument => argument.UpdatedAt)
            .Take(12)
            .Select(argument => new WorldviewInsightArgument(
                argument.Id.ToString(),
                "SocialView",
                "Detail",
                argument.Title,
                argument.ClaimProposition != null ? argument.ClaimProposition.Text : argument.WarrantText,
                argument.SchwartzValues,
                argument.UpdatedAt))
            .ToListAsync(ct);

        var arguments = primaryArguments
            .Concat(socialArguments)
            .OrderByDescending(argument => argument.SubmittedAt)
            .Take(12)
            .ToList();
        var communityViews = await BuildCommunityViewsAsync(db, userId, arguments, ct);

        if (snapshot is null)
        {
            var argumentSummary = await _summaryService.GenerateArgumentSummaryAsync(arguments, ct);
            return new WorldviewInsight
            {
                SubmittedArgumentCount = arguments.Count,
                Arguments = arguments,
                CommunityViews = communityViews,
                Summary = argumentSummary,
                MappingNarrative = "Complete a few discovery questions to place your perspective in relation to other people and intellectual traditions."
            };
        }

        var dimensions = snapshot.Dimensions
            .Where(dimension => dimension.Position.HasValue)
            .OrderByDescending(dimension => dimension.Confidence)
            .Take(8)
            .Select(dimension => new WorldviewInsightDimension(
                dimension.Name,
                dimension.Category,
                dimension.Position!.Value,
                dimension.Confidence))
            .ToList();

        var horizontal = dimensions.ElementAtOrDefault(0)?.Name ?? "Political orientation";
        var vertical = dimensions.ElementAtOrDefault(1)?.Name ?? horizontal;
        var peers = BuildPeerMap(userId, snapshot, horizontal, vertical);
        var matches = _knowledgeBase.CompareUserToCanonicalSystems(snapshot, 5)
            .Select(match => new WorldviewInsightMatch(
                match.SystemName,
                match.SystemCategory,
                match.OverallMatchPercentage,
                match.SharedValues.Take(4).ToArray(),
                match.KeyDifferences.Take(2).ToArray()))
            .ToList();

        var summary = await _summaryService.GenerateIntegratedSummaryAsync(snapshot, arguments, peers, ct);

        return new WorldviewInsight
        {
            HasBeliefProfile = true,
            InteractionCount = snapshot.InteractionCount,
            SubmittedArgumentCount = arguments.Count,
            ProfileConfidence = snapshot.OverallConfidence,
            Summary = summary,
            MappingNarrative = BuildMappingNarrative(peers, matches),
            TopValues = snapshot.Values
                .OrderByDescending(value => value.ImportanceScore * value.Confidence)
                .Take(6)
                .Select(value => new WorldviewInsightValue(value.Name, value.ImportanceScore, value.Confidence))
                .ToList(),
            Dimensions = dimensions,
            Arguments = arguments,
            CommunityViews = communityViews,
            CanonicalMatches = matches,
            Peers = peers,
            HorizontalAxis = horizontal,
            VerticalAxis = vertical,
            UserX = GetPosition(snapshot, horizontal),
            UserY = GetPosition(snapshot, vertical),
            MoralFoundations = new[]
            {
                snapshot.MoralFoundations.Care.Score,
                snapshot.MoralFoundations.Fairness.Score,
                snapshot.MoralFoundations.Loyalty.Score,
                snapshot.MoralFoundations.Authority.Score,
                snapshot.MoralFoundations.Sanctity.Score,
                snapshot.MoralFoundations.Liberty.Score
            }
        };
    }

    private async Task<List<WorldviewCommunityView>> BuildCommunityViewsAsync(
        ApplicationDbContext db,
        string userId,
        IReadOnlyList<WorldviewInsightArgument> arguments,
        CancellationToken ct)
    {
        if (arguments.Count == 0)
            return new();

        var communityAuthorCount = await db.UserAccounts
            .AsNoTracking()
            .CountAsync(account => account.IsActive && !account.IsServiceAccount && account.Id != userId, ct);
        var candidates = await db.SocialArguments
            .AsNoTracking()
            .Include(argument => argument.ClaimProposition)
            .Where(argument => argument.IsPublic
                && !argument.IsShadowBanned
                && !argument.IsAIGenerated
                && argument.UserId != userId)
            .OrderByDescending(argument => argument.UpdatedAt)
            .Take(500)
            .Select(argument => new CommunityArgumentCandidate(
                argument.UserId,
                argument.Title,
                argument.ClaimProposition != null ? argument.ClaimProposition.Text : argument.WarrantText,
                argument.Embedding))
            .ToListAsync(ct);

        var primaryIds = arguments
            .Where(argument => argument.DetailController == "Argument")
            .Select(argument => int.TryParse(argument.Id, out var id) ? (int?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        var socialIds = arguments
            .Where(argument => argument.DetailController == "SocialView")
            .Select(argument => Guid.TryParse(argument.Id, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        var publishedArguments = await db.SocialArguments
            .AsNoTracking()
            .Include(argument => argument.Votes)
            .Where(argument => argument.UserId == userId
                && argument.IsPublic
                && !argument.IsShadowBanned
                && (socialIds.Contains(argument.Id)
                    || (argument.SourceArgumentId.HasValue && primaryIds.Contains(argument.SourceArgumentId.Value))))
            .ToListAsync(ct);

        var results = new List<WorldviewCommunityView>();
        foreach (var argument in arguments.Take(8))
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync($"{argument.Title}\n{argument.Claim}", ct);
            var similar = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Similarity = embedding is not null && candidate.Embedding is not null
                        ? ScoringAlgorithms.CosineSimilarity(embedding, candidate.Embedding)
                        : LexicalSimilarity(argument, candidate)
                })
                .Where(match => match.Similarity >= 0.72)
                .OrderByDescending(match => match.Similarity)
                .ToList();
            var similarAuthorCount = similar.Select(match => match.Candidate.UserId).Distinct().Count();
            var exactPublishedArgument = argument.DetailController == "SocialView"
                && Guid.TryParse(argument.Id, out var socialArgumentId)
                    ? publishedArguments.FirstOrDefault(published => published.Id == socialArgumentId)
                    : int.TryParse(argument.Id, out var primaryArgumentId)
                        ? publishedArguments.FirstOrDefault(published => published.SourceArgumentId == primaryArgumentId)
                        : null;
            var decisiveVotes = exactPublishedArgument?.Votes
                .Where(vote => vote.Vote is VoteValue.Up or VoteValue.Down)
                .ToList() ?? new();
            var upWeight = decisiveVotes.Where(vote => vote.Vote == VoteValue.Up).Sum(vote => vote.EpistemicWeight);
            var downWeight = decisiveVotes.Where(vote => vote.Vote == VoteValue.Down).Sum(vote => vote.EpistemicWeight);
            var decisiveWeight = upWeight + downWeight;
            var supportShare = decisiveWeight > 0 ? upWeight / decisiveWeight : (double?)null;

            results.Add(new WorldviewCommunityView(
                argument.Id,
                argument.Title,
                ClassifyInterest(similarAuthorCount, communityAuthorCount),
                similar.Count,
                similarAuthorCount,
                communityAuthorCount,
                ClassifyOpinion(supportShare, decisiveVotes.Count, exactPublishedArgument is not null),
                supportShare,
                decisiveVotes.Count,
                BuildConfidenceNote(embedding is not null, similar.Count, decisiveVotes.Count, exactPublishedArgument is not null)));
        }

        return results;
    }

    private static string ClassifyInterest(int similarAuthors, int communityAuthors)
    {
        if (communityAuthors == 0) return "Community baseline unavailable";
        var share = (double)similarAuthors / communityAuthors;
        return share switch
        {
            >= 0.5 => "Very common interest",
            >= 0.2 => "Common interest",
            >= 0.05 => "Niche interest",
            _ => "Idiosyncratic interest"
        };
    }

    private static string ClassifyOpinion(double? supportShare, int voteCount, bool isPublished)
    {
        if (!isPublished) return "Not measured until published";
        if (!supportShare.HasValue || voteCount < 3) return "Not enough community votes";
        return supportShare.Value switch
        {
            >= 0.67 => "Majority-supported opinion",
            >= 0.55 => "Leans supported",
            > 0.45 => "Community divided",
            > 0.33 => "Leans opposed",
            _ => "Minority opinion"
        };
    }

    private static string BuildConfidenceNote(bool semanticMatch, int similarCount, int voteCount, bool isPublished)
    {
        var method = semanticMatch ? "semantic similarity" : "shared terms";
        var topicNote = similarCount == 0
            ? $"No comparable community arguments found using {method}."
            : $"Topic estimate uses {similarCount} comparable community argument{(similarCount == 1 ? "" : "s")} found by {method}.";
        var opinionNote = !isPublished
            ? " Opinion support is not measured because this argument has not been published."
            : $" Opinion support uses {voteCount} decisive vote{(voteCount == 1 ? "" : "s")} on this exact published argument.";
        return topicNote + opinionNote;
    }

    private static double LexicalSimilarity(WorldviewInsightArgument argument, CommunityArgumentCandidate candidate)
    {
        var first = Tokenize($"{argument.Title} {argument.Claim}");
        var second = Tokenize($"{candidate.Title} {candidate.Claim}");
        if (first.Count == 0 || second.Count == 0) return 0;
        return (double)first.Intersect(second).Count() / first.Union(second).Count();
    }

    private static HashSet<string> Tokenize(string text) => text
        .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(word => word.Length >= 4)
        .Select(word => word.ToLowerInvariant())
        .ToHashSet();

    private sealed record CommunityArgumentCandidate(
        string UserId,
        string Title,
        string Claim,
        float[]? Embedding);

    private List<WorldviewInsightPeer> BuildPeerMap(
        string userId,
        BeliefSnapshot snapshot,
        string horizontal,
        string vertical)
    {
        return _profileStore.GetAllProfiles()
            .Where(profile => profile.Id != userId && profile.CurrentBeliefSnapshot is not null)
            .Select(profile => new
            {
                Snapshot = profile.CurrentBeliefSnapshot!,
                Similarity = CalculateSimilarity(snapshot, profile.CurrentBeliefSnapshot!)
            })
            .OrderByDescending(peer => peer.Similarity)
            .Take(30)
            .Select((peer, index) => new WorldviewInsightPeer(
                $"Community member {index + 1}",
                peer.Similarity,
                GetPosition(peer.Snapshot, horizontal),
                GetPosition(peer.Snapshot, vertical)))
            .ToList();
    }

    private static double CalculateSimilarity(BeliefSnapshot first, BeliefSnapshot second)
    {
        var secondDimensions = second.Dimensions
            .Where(dimension => dimension.Position.HasValue)
            .ToDictionary(dimension => dimension.Name, StringComparer.OrdinalIgnoreCase);

        var comparable = first.Dimensions
            .Where(dimension => dimension.Position.HasValue && secondDimensions.ContainsKey(dimension.Name))
            .Select(dimension =>
            {
                var other = secondDimensions[dimension.Name];
                var confidence = Math.Min(dimension.Confidence, other.Confidence);
                var closeness = 1.0 - Math.Abs(dimension.Position!.Value - other.Position!.Value) / 2.0;
                return (Closeness: Math.Clamp(closeness, 0, 1), Weight: confidence);
            })
            .Where(item => item.Weight > 0)
            .ToList();

        return comparable.Count == 0
            ? 0
            : comparable.Sum(item => item.Closeness * item.Weight) / comparable.Sum(item => item.Weight);
    }

    private static double GetPosition(BeliefSnapshot snapshot, string dimensionName)
    {
        return snapshot.Dimensions.FirstOrDefault(dimension =>
            dimension.Name.Equals(dimensionName, StringComparison.OrdinalIgnoreCase))?.Position ?? 0;
    }

    private static string BuildMappingNarrative(
        IReadOnlyList<WorldviewInsightPeer> peers,
        IReadOnlyList<WorldviewInsightMatch> matches)
    {
        if (peers.Count == 0 && matches.Count == 0)
            return "There is not enough comparison data yet to locate your worldview relative to the community.";

        var peerText = peers.Count == 0
            ? "Your community neighborhood is still forming"
            : $"Your closest anonymized community perspectives currently align at {peers[0].Similarity:P0}";
        var traditionText = matches.Count == 0
            ? "no canonical tradition has a strong enough signal yet"
            : $"your strongest historical affinity is {matches[0].Name} at {matches[0].Match:P0}";

        return $"{peerText}, while {traditionText}. The map shows proximity, not identity: nearby points share measured tendencies, while distance highlights useful differences rather than opposition.";
    }
}