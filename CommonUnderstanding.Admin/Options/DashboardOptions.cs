namespace CommonUnderstanding.Admin.Options;

public sealed class DashboardOptions
{
    public int DefaultRangeHours { get; set; } = 24;
    public int PlatformCacheMinutes { get; set; } = 5;
    public int AdoptionCacheMinutes { get; set; } = 15;
    public int ActiveUserWindowMinutes { get; set; } = 15;
}