namespace CommonUnderstanding.Tests.Architecture;

public sealed class RequestPipelineOrderingTests
{
    [Fact]
    public void ActivityTracking_IsAfterStaticFilesAndBeforeOutputCache()
    {
        var programSource = File.ReadAllText(RepositoryFile("CommonUnderstanding", "Program.cs"));
        var lastStaticFiles = programSource.LastIndexOf("app.UseStaticFiles", StringComparison.Ordinal);
        var recordActivity = programSource.IndexOf("RecordActivity()", StringComparison.Ordinal);
        var outputCache = programSource.IndexOf("app.UseOutputCache()", StringComparison.Ordinal);

        Assert.True(lastStaticFiles >= 0, "Static-file middleware was not found.");
        Assert.True(recordActivity > lastStaticFiles,
            "Activity tracking must run after static files so asset requests do not wake workers.");
        Assert.True(outputCache > recordActivity,
            "Activity tracking must run before output caching so cached dynamic requests count as activity.");
    }

    private static string RepositoryFile(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            Path.Combine(segments)));
    }
}
