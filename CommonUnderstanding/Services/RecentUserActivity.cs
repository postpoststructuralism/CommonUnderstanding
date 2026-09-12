namespace CommonUnderstanding.Services;

public sealed class RecentUserActivity
{
    private readonly TimeSpan _activeWindow;
    private readonly TimeProvider _timeProvider;
    private long _lastActivityUtcTicks;

    public RecentUserActivity(IConfiguration configuration, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        var activeWindowMinutes = Math.Max(
            1,
            configuration.GetValue("BackgroundWorkers:ActiveUserWindowMinutes", 15));
        _activeWindow = TimeSpan.FromMinutes(activeWindowMinutes);
    }

    public bool IsActive
    {
        get
        {
            var lastActivityUtcTicks = Interlocked.Read(ref _lastActivityUtcTicks);
            return lastActivityUtcTicks != 0
                && _timeProvider.GetUtcNow().UtcDateTime
                    - new DateTime(lastActivityUtcTicks, DateTimeKind.Utc) <= _activeWindow;
        }
    }

    public void RecordActivity()
    {
        Interlocked.Exchange(ref _lastActivityUtcTicks, _timeProvider.GetUtcNow().UtcTicks);
    }
}