using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommonUnderstanding.Services;

/// <summary>
/// Centralized thread-safe storage for user profiles and pending interactions.
/// Profiles are persisted to SQLite so identities survive app restarts.
/// An in-memory cache is kept for performance; the DB is the source of truth.
/// Uses IServiceScopeFactory to create short-lived scopes for DB access,
/// which is the correct pattern for singleton services that need scoped DB contexts.
/// </summary>
public class UserProfileStore
{
    private readonly ConcurrentDictionary<string, UserProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, UserInteraction> _pendingInteractions = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserProfileStore> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public UserProfileStore(IServiceScopeFactory scopeFactory, ILogger<UserProfileStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ---- Profile management ----

    public void AddProfile(UserProfile profile)
    {
        if (_profiles.TryAdd(profile.Id, profile))
        {
            _logger.LogInformation("Added profile {ProfileId} for {Name}", profile.Id, profile.Name);
            _ = Task.Run(() => PersistAsync(profile));
        }
        else
        {
            _logger.LogWarning("Profile {ProfileId} already exists", profile.Id);
        }
    }

    public UserProfile? GetProfile(string profileId)
    {
        if (_profiles.TryGetValue(profileId, out var cached))
            return cached;

        // Cache miss — attempt to load from DB (handles returning users after restart)
        var profile = LoadFromDb(profileId);
        if (profile is not null)
            _profiles.TryAdd(profile.Id, profile);
        return profile;
    }

    public bool ProfileExists(string profileId)
    {
        if (_profiles.ContainsKey(profileId)) return true;
        return GetProfile(profileId) is not null;
    }

    public IEnumerable<UserProfile> GetAllProfiles()
    {
        // Ensure any DB profiles not yet cached are loaded
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ids = db.UserProfiles.Select(p => p.Id).ToList();
            foreach (var id in ids.Where(id => !_profiles.ContainsKey(id)))
            {
                var entity = db.UserProfiles.Find(id);
                if (entity is not null)
                    _profiles.TryAdd(id, Reconstruct(entity));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load all profiles from DB");
        }

        return _profiles.Values;
    }

    /// <summary>
    /// Persists the current in-memory state of a profile back to the database.
    /// Call this after significant profile mutations (e.g. after belief analysis completes).
    /// </summary>
    public async Task SaveProfileAsync(string profileId)
    {
        if (!_profiles.TryGetValue(profileId, out var profile)) return;
        await PersistAsync(profile);
    }

    // ---- Pending interaction management ----

    public void SetPendingInteraction(string profileId, UserInteraction interaction)
    {
        _pendingInteractions[profileId] = interaction;
        _logger.LogDebug("Set pending interaction for profile {ProfileId}", profileId);
    }

    public UserInteraction? GetPendingInteraction(string profileId)
    {
        _pendingInteractions.TryGetValue(profileId, out var interaction);
        return interaction;
    }

    public bool ClearPendingInteraction(string profileId)
    {
        return _pendingInteractions.TryRemove(profileId, out _);
    }

    // ---- Private persistence helpers ----

    private async Task PersistAsync(UserProfile profile)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existing = await db.UserProfiles.FindAsync(profile.Id);
            var entity = ToEntity(profile);

            if (existing is null)
            {
                db.UserProfiles.Add(entity);
            }
            else
            {
                existing.Name = entity.Name;
                existing.LastInteractionAt = entity.LastInteractionAt;
                existing.Stage = entity.Stage;
                existing.CurrentBeliefSnapshotJson = entity.CurrentBeliefSnapshotJson;
                existing.HistoricalSnapshotsJson = entity.HistoricalSnapshotsJson;
                existing.InteractionsJson = entity.InteractionsJson;
                existing.AskedQuestionHashesJson = entity.AskedQuestionHashesJson;
                existing.ExploredDimensionsJson = entity.ExploredDimensionsJson;
            }

            await db.SaveChangesAsync();
            _logger.LogDebug("Persisted profile {ProfileId}", profile.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist profile {ProfileId}", profile.Id);
        }
    }

    private UserProfile? LoadFromDb(string profileId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entity = db.UserProfiles.Find(profileId);
            return entity is null ? null : Reconstruct(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile {ProfileId} from DB", profileId);
            return null;
        }
    }

    private static PersistedUserProfile ToEntity(UserProfile p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        CreatedAt = p.CreatedAt,
        LastInteractionAt = p.LastInteractionAt,
        Stage = p.Stage.ToString(),
        CurrentBeliefSnapshotJson = p.CurrentBeliefSnapshot is null
            ? null
            : JsonSerializer.Serialize(p.CurrentBeliefSnapshot, _json),
        HistoricalSnapshotsJson = JsonSerializer.Serialize(p.HistoricalSnapshots, _json),
        InteractionsJson = JsonSerializer.Serialize(p.Interactions, _json),
        AskedQuestionHashesJson = JsonSerializer.Serialize(p.AskedQuestionHashes, _json),
        ExploredDimensionsJson = JsonSerializer.Serialize(p.ExploredDimensions, _json),
    };

    private static UserProfile Reconstruct(PersistedUserProfile e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        CreatedAt = e.CreatedAt,
        LastInteractionAt = e.LastInteractionAt,
        Stage = Enum.TryParse<DiscoveryStage>(e.Stage, out var stage) ? stage : DiscoveryStage.Initial,
        CurrentBeliefSnapshot = e.CurrentBeliefSnapshotJson is null
            ? null
            : JsonSerializer.Deserialize<BeliefSnapshot>(e.CurrentBeliefSnapshotJson, _json),
        HistoricalSnapshots = JsonSerializer.Deserialize<List<BeliefSnapshot>>(
            e.HistoricalSnapshotsJson, _json) ?? new(),
        Interactions = JsonSerializer.Deserialize<List<UserInteraction>>(
            e.InteractionsJson, _json) ?? new(),
        AskedQuestionHashes = JsonSerializer.Deserialize<HashSet<string>>(
            e.AskedQuestionHashesJson, _json) ?? new(),
        ExploredDimensions = JsonSerializer.Deserialize<HashSet<string>>(
            e.ExploredDimensionsJson, _json) ?? new(),
        // PrefetchedQuestions is transient — regenerated each session
    };
}
