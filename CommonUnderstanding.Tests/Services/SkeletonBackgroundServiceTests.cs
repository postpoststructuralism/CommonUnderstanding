using System.Text.Json;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Tests.Services;

public sealed class SkeletonBackgroundServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"common-understanding-skeleton-tests-{Guid.NewGuid():N}");

    [Fact]
    public void GetNextRefresh_UsesManifestAgeAcrossProcessRestarts()
    {
        var generatedAt = new DateTimeOffset(2026, 9, 20, 3, 0, 0, TimeSpan.Zero);
        var now = generatedAt.AddHours(6);
        var manifestPath = WriteManifest(generatedAt);

        var nextRefresh = SkeletonBackgroundService.GetNextRefresh(
            manifestPath, now, TimeSpan.FromHours(24));

        Assert.Equal(generatedAt.AddHours(24), nextRefresh);
    }

    [Fact]
    public void GetNextRefresh_ReturnsNowWhenManifestIsStale()
    {
        var generatedAt = new DateTimeOffset(2026, 9, 20, 3, 0, 0, TimeSpan.Zero);
        var now = generatedAt.AddHours(25);
        var manifestPath = WriteManifest(generatedAt);

        var nextRefresh = SkeletonBackgroundService.GetNextRefresh(
            manifestPath, now, TimeSpan.FromHours(24));

        Assert.Equal(now, nextRefresh);
    }

    [Fact]
    public void GetNextRefresh_ReturnsNowWhenManifestIsMissing()
    {
        var now = new DateTimeOffset(2026, 9, 20, 3, 0, 0, TimeSpan.Zero);

        var nextRefresh = SkeletonBackgroundService.GetNextRefresh(
            Path.Combine(_tempDirectory, "missing.json"), now, TimeSpan.FromHours(24));

        Assert.Equal(now, nextRefresh);
    }

    private string WriteManifest(DateTimeOffset generatedAt)
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "skeleton-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { generatedAt }));
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}