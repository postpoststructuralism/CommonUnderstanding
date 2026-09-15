using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Provenance;

namespace CommonUnderstanding.Tests.Services;

public sealed class EvidenceMatchingServiceTests
{
    [Fact]
    public void SelectCandidates_ReturnsThreeHighestSimilarityUnseenEntriesPerRun()
    {
        var corpus = Enumerable.Range(1, 6)
            .Select(id => new EvidenceCorpusEntry
            {
                Id = id,
                Embedding = [1, (7 - id) / 10f]
            })
            .ToList();

        var firstBatch = EvidenceMatchingService.SelectCandidates(corpus, [1, 1]);
        var secondBatch = EvidenceMatchingService.SelectCandidates(
            corpus.Where(entry => firstBatch.All(candidate => candidate.Entry.Id != entry.Id)),
            [1, 1]);

        Assert.Equal([1L, 2L, 3L], firstBatch.Select(candidate => candidate.Entry.Id));
        Assert.Equal([4L, 5L, 6L], secondBatch.Select(candidate => candidate.Entry.Id));
    }

    [Fact]
    public void SelectCandidates_RespectsRemainingPendingSuggestionSlots()
    {
        var corpus = Enumerable.Range(1, 4)
            .Select(id => new EvidenceCorpusEntry
            {
                Id = id,
                Embedding = [1, (5 - id) / 10f]
            });

        var candidates = EvidenceMatchingService.SelectCandidates(corpus, [1, 1], limit: 1);

        Assert.Single(candidates);
        Assert.Equal(1, candidates[0].Entry.Id);
    }
}