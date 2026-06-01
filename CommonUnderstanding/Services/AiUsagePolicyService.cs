using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

public sealed class AiAccessDeniedException : Exception
{
    public AiAccessDeniedException(string message) : base(message) { }
}

/// <summary>
/// Lightweight in-memory request gating for launch paywall behavior.
/// Core users are unlimited; everyone else gets a capped number of AI requests.
/// </summary>
public sealed class AiUsagePolicyService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly bool _enabled;
    private readonly int _freeRequestLimit;
    private readonly HashSet<string> _coreUserIds;
    private readonly HashSet<string> _coreUsernames;
    private readonly bool _countAnonymous;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AiUsagePolicyService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _scopeFactory = scopeFactory;
        _enabled = bool.TryParse(configuration["AiAccessPolicy:Enabled"], out var enabled) ? enabled : true;
        _freeRequestLimit = int.TryParse(configuration["AiAccessPolicy:FreeAiRequestLimit"], out var limit) ? limit : 120;
        _countAnonymous = bool.TryParse(configuration["AiAccessPolicy:CountAnonymous"], out var countAnonymous) ? countAnonymous : true;

        _coreUserIds = (configuration.GetSection("AiAccessPolicy:CoreUserIds").Get<string[]>() ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _coreUsernames = (configuration.GetSection("AiAccessPolicy:CoreUsernames").Get<string[]>() ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _httpContextAccessor = httpContextAccessor;
    }

    public async Task EnforceAndTrackAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
            return;

        var context = _httpContextAccessor.HttpContext;
        var userId = context?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = context?.User?.FindFirst("username")?.Value?.Trim().ToLowerInvariant();

        if (IsCoreUser(userId, username))
            return;

        if (string.IsNullOrWhiteSpace(userId))
        {
            if (!_countAnonymous)
                return;

            var ip = context?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            userId = $"anon:{ip}";
        }

        var counterKey = userId.Trim();
        var now = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var counter = await db.AiUsageCounters
            .FirstOrDefaultAsync(x => x.CounterKey == counterKey, cancellationToken);

        int nextCount;
        if (counter is null)
        {
            counter = new Models.AiUsageCounter
            {
                CounterKey = counterKey,
                RequestCount = 1,
                CreatedAt = now,
                LastRequestAt = now
            };
            db.AiUsageCounters.Add(counter);
            nextCount = 1;
        }
        else
        {
            counter.RequestCount += 1;
            counter.LastRequestAt = now;
            nextCount = counter.RequestCount;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (nextCount > _freeRequestLimit)
        {
            throw new AiAccessDeniedException(
                $"Free-tier AI usage limit reached ({_freeRequestLimit} requests). Please upgrade to continue.");
        }
    }

    private bool IsCoreUser(string? userId, string? username)
    {
        if (!string.IsNullOrWhiteSpace(userId) && _coreUserIds.Contains(userId.Trim()))
            return true;

        if (!string.IsNullOrWhiteSpace(username) && _coreUsernames.Contains(username.Trim().ToLowerInvariant()))
            return true;

        return false;
    }
}
