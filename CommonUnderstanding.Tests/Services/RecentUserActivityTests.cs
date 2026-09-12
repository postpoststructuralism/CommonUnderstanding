using CommonUnderstanding.Services;
using Microsoft.Extensions.Configuration;

namespace CommonUnderstanding.Tests.Services;

public sealed class RecentUserActivityTests
{
    [Fact]
    public void IsActive_IsFalseBeforeActivityIsRecorded()
    {
        var activity = CreateActivity(new ManualTimeProvider());

        Assert.False(activity.IsActive);
    }

    [Fact]
    public void RecordActivity_ActivatesConfiguredWindowAndExpiresAfterBoundary()
    {
        var clock = new ManualTimeProvider();
        var activity = CreateActivity(clock, activeWindowMinutes: 15);

        activity.RecordActivity();
        Assert.True(activity.IsActive);

        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.True(activity.IsActive);

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(activity.IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ActiveWindow_IsClampedToOneMinute(int configuredMinutes)
    {
        var clock = new ManualTimeProvider();
        var activity = CreateActivity(clock, configuredMinutes);

        activity.RecordActivity();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(activity.IsActive);

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(activity.IsActive);
    }

    private static RecentUserActivity CreateActivity(
        TimeProvider timeProvider,
        int activeWindowMinutes = 15)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundWorkers:ActiveUserWindowMinutes"] = activeWindowMinutes.ToString()
            })
            .Build();

        return new RecentUserActivity(configuration, timeProvider);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
