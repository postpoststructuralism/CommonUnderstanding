namespace CommonUnderstanding.Admin.Models;

public sealed record DashboardSnapshot(
    DateTimeOffset GeneratedAt,
    int RangeHours,
    AdoptionSnapshot Adoption,
    AiComputeSnapshot AiCompute,
    EndpointTrafficSnapshot EndpointTraffic,
    PlatformSnapshot Platform,
    AvailabilitySnapshot Availability,
    IReadOnlyList<string> Warnings);

public sealed record EndpointTrafficSnapshot(
    bool IsAvailable,
    IReadOnlyList<EndpointRequestCount> Endpoints,
    long CapturedRequests,
    DateTimeOffset? FirstObservedAt,
    DateTimeOffset? LastObservedAt,
    string? Error = null)
{
    public static EndpointTrafficSnapshot Unavailable(string error) => new(false, [], 0, null, null, error);
}

public sealed record EndpointRequestCount(
    string Method,
    string Path,
    long Requests,
    long ServerErrors,
    double AverageDurationMilliseconds);

public sealed record AdoptionSnapshot(
    bool IsAvailable,
    int TotalUsers,
    int ActiveUsers,
    int NewUsers,
    int Arguments,
    int Votes,
    long WidgetViews,
    string? Error = null)
{
    public static AdoptionSnapshot Unavailable(string error) => new(false, 0, 0, 0, 0, 0, 0, error);
}

public sealed record AiComputeSnapshot(
    bool IsAvailable,
    MetricSeries Requests,
    MetricSeries InputTokens,
    MetricSeries OutputTokens,
    MetricSeries TotalTokens,
    string? Error = null)
{
    public static AiComputeSnapshot Unavailable(string error) => new(
        false,
        MetricSeries.Empty("Model requests"),
        MetricSeries.Empty("Input tokens"),
        MetricSeries.Empty("Output tokens"),
        MetricSeries.Empty("Total tokens"),
        error);
}

public sealed record PlatformSnapshot(
    bool IsAvailable,
    MetricSeries Requests,
    MetricSeries AverageResponseTime,
    MetricSeries ServerErrors,
    MetricSeries CpuTime,
    MetricSeries MemoryWorkingSet,
    MetricSeries DatabaseCpu,
    MetricSeries DatabaseDataIo,
    MetricSeries DatabaseLogIo,
    MetricSeries DatabaseStorage,
    MetricSeries DatabaseSessions,
    string? Error = null)
{
    public static PlatformSnapshot Unavailable(string error) => new(
        false, MetricSeries.Empty("Requests"), MetricSeries.Empty("Average response time"),
        MetricSeries.Empty("HTTP 5xx"), MetricSeries.Empty("CPU time"),
        MetricSeries.Empty("Memory working set"), MetricSeries.Empty("Database CPU"),
        MetricSeries.Empty("Database data I/O"), MetricSeries.Empty("Database log I/O"),
        MetricSeries.Empty("Database storage"), MetricSeries.Empty("Database sessions"), error);
}

public sealed record MetricSeries(string Name, string Unit, double Total, double Average, double Maximum, IReadOnlyList<MetricPoint> Points)
{
    public static MetricSeries Empty(string name) => new(name, string.Empty, 0, 0, 0, []);
}

public sealed record MetricPoint(DateTimeOffset Timestamp, double Value);

public sealed record AvailabilitySnapshot(
    bool IsReachable,
    int StatusCode,
    double LatencyMilliseconds,
    DateTimeOffset CheckedAt,
    DateTimeOffset MonitoringStartedAt,
    double ObservedAvailabilityPercent,
    int SuccessfulChecks,
    int FailedChecks,
    DateTimeOffset? LastFailureAt,
    string? Error);