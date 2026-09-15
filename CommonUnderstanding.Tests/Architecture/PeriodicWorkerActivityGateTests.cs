namespace CommonUnderstanding.Tests.Architecture;

public sealed class PeriodicWorkerActivityGateTests
{
    public static TheoryData<string, string> PeriodicWorkers => new()
    {
        { "SchemaDiscoveryWorker", Path.Combine("Services", "SchemaDiscoveryWorker.cs") },
        { "SkeletonBackgroundService", Path.Combine("Services", "SkeletonBackgroundService.cs") },
        { "HotScoreUpdateWorker", Path.Combine("Services", "Social", "Workers", "HotScoreUpdateWorker.cs") },
        { "EpistemicScoringWorker", Path.Combine("Services", "Social", "Workers", "EpistemicScoringWorker.cs") },
        { "AIValidationWorker", Path.Combine("Services", "Social", "Workers", "AIValidationWorker.cs") },
        { "EmbeddingBackfillWorker", Path.Combine("Services", "Social", "Workers", "EmbeddingBackfillWorker.cs") },
        { "ReplyCountWorker", Path.Combine("Services", "Social", "Workers", "ReplyCountWorker.cs") },
        { "DmiScoreWorker", Path.Combine("Services", "Social", "Workers", "DmiScoreWorker.cs") },
        { "BaselineContentWorker", Path.Combine("Services", "Social", "Workers", "BaselineContentWorker.cs") },
        { "CrossThreadContradictionWorker", Path.Combine("Services", "Widget", "CrossThreadContradictionWorker.cs") }
    };

    [Theory]
    [MemberData(nameof(PeriodicWorkers))]
    public void PeriodicDatabaseWorker_IsRegisteredAndChecksActivityBeforeDatabaseAccess(
        string workerName,
        string relativePath)
    {
        var programSource = File.ReadAllText(RepositoryFile("CommonUnderstanding", "Program.cs"));
        Assert.Contains($"AddHostedService<{workerName}>", programSource, StringComparison.Ordinal);

        var workerSource = File.ReadAllText(RepositoryFile("CommonUnderstanding", relativePath));
        Assert.Contains("RecentUserActivity", workerSource, StringComparison.Ordinal);

        var gate = workerSource.IndexOf("_userActivity.ShouldRunBackgroundWork", StringComparison.Ordinal);
        Assert.True(gate >= 0, $"{workerName} does not wait for the user activity window to expire.");

        var databaseAccess = FirstIndexAfter(workerSource, 0,
            "CreateScope(",
            "CreateAsyncScope(",
            "CreateDbContext(",
            "CreateDbContextAsync(");

        Assert.True(databaseAccess < 0 || gate < databaseAccess,
            $"{workerName} accesses a database scope or context before checking user activity.");
    }

    [Fact]
    public void ResponseProcessingQueue_RemainsUngatedForAcceptedUserWork()
    {
        var programSource = File.ReadAllText(RepositoryFile("CommonUnderstanding", "Program.cs"));
        var queueSource = File.ReadAllText(
            RepositoryFile("CommonUnderstanding", "Services", "ResponseProcessingQueue.cs"));

        Assert.Contains("AddHostedService", programSource, StringComparison.Ordinal);
        Assert.Contains("ResponseProcessingQueue", programSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RecentUserActivity", queueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_userActivity.ShouldRunBackgroundWork", queueSource, StringComparison.Ordinal);
    }

    private static int FirstIndexAfter(string source, int startIndex, params string[] values)
    {
        return values
            .Select(value => source.IndexOf(value, startIndex, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static string RepositoryFile(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            Path.Combine(segments)));
    }
}
