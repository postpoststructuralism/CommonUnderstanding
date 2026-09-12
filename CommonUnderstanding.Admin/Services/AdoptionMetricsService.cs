using CommonUnderstanding.Admin.Models;
using CommonUnderstanding.Admin.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CommonUnderstanding.Admin.Services;

public sealed class AdoptionMetricsService(
    IConfiguration configuration,
    IMemoryCache cache,
    IOptions<DashboardOptions> options,
    ILogger<AdoptionMetricsService> logger)
{
    public async Task<AdoptionSnapshot> GetAsync(int rangeHours, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("ReadOnlyDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return AdoptionSnapshot.Unavailable(
                "Set ConnectionStrings:ReadOnlyDatabase with dotnet user-secrets to enable adoption metrics.");
        }

        var cacheKey = $"adoption:{rangeHours}";
        if (cache.TryGetValue(cacheKey, out AdoptionSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = Math.Min(new SqlConnectionStringBuilder(connectionString).ConnectTimeout, 15)
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                DECLARE @Since datetime2 = DATEADD(hour, -@RangeHours, SYSUTCDATETIME());
                DECLARE @ActiveSince datetime2 = DATEADD(minute, -@ActiveMinutes, SYSUTCDATETIME());

                SELECT COUNT_BIG(*) FROM UserAccounts WHERE IsServiceAccount = 0;
                SELECT COUNT_BIG(*) FROM UserAccounts WHERE IsServiceAccount = 0 AND CreatedAt >= @Since;
                SELECT COUNT_BIG(DISTINCT Activity.UserId)
                FROM (
                    SELECT Id AS UserId FROM UserProfiles WHERE LastInteractionAt >= @ActiveSince
                    UNION
                    SELECT UserId FROM UserReputations WHERE LastActiveAt >= @ActiveSince
                ) Activity
                INNER JOIN UserAccounts u ON u.Id = Activity.UserId AND u.IsServiceAccount = 0;
                SELECT COUNT_BIG(*) FROM SocialArguments WHERE CreatedAt >= @Since AND IsAIGenerated = 0;
                SELECT COUNT_BIG(*) FROM ArgumentVotes WHERE CreatedAt >= @Since;
                SELECT COALESCE(SUM(PageViews), 0) FROM WidgetUsages WHERE [Date] >= CONVERT(date, @Since);
                """;

            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
            command.Parameters.AddWithValue("@RangeHours", rangeHours);
            command.Parameters.AddWithValue("@ActiveMinutes", Math.Max(1, options.Value.ActiveUserWindowMinutes));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var values = new long[6];
            for (var index = 0; index < values.Length; index++)
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("The adoption query returned an incomplete result.");
                }

                values[index] = reader.GetInt64(0);
                if (index < values.Length - 1)
                {
                    await reader.NextResultAsync(cancellationToken);
                }
            }

            var result = new AdoptionSnapshot(true, ToInt(values[0]), ToInt(values[2]), ToInt(values[1]),
                ToInt(values[3]), ToInt(values[4]), values[5]);
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, options.Value.AdoptionCacheMinutes)));
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not load adoption metrics");
            return AdoptionSnapshot.Unavailable(exception.Message);
        }
    }

    private static int ToInt(long value) => (int)Math.Min(value, int.MaxValue);
}