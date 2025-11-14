using CommonUnderstanding.Models;
using System.Collections.Concurrent;

namespace CommonUnderstanding.Services;

/// <summary>
/// Centralized thread-safe storage for user profiles and pending interactions.
/// This ensures Controller and SignalR Hub share the same data.
/// </summary>
public class UserProfileStore
{
    private readonly ConcurrentDictionary<string, UserProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, UserInteraction> _pendingInteractions = new();
    private readonly ILogger<UserProfileStore> _logger;

    public UserProfileStore(ILogger<UserProfileStore> logger)
    {
        _logger = logger;
    }

    // Profile management
    public void AddProfile(UserProfile profile)
    {
        if (_profiles.TryAdd(profile.Id, profile))
        {
            _logger.LogInformation("Added profile {ProfileId} for {Name}", profile.Id, profile.Name);
        }
        else
        {
            _logger.LogWarning("Profile {ProfileId} already exists", profile.Id);
        }
    }

    public UserProfile? GetProfile(string profileId)
    {
        _profiles.TryGetValue(profileId, out var profile);
        return profile;
    }

    public bool ProfileExists(string profileId)
    {
        return _profiles.ContainsKey(profileId);
    }

    public IEnumerable<UserProfile> GetAllProfiles()
    {
        return _profiles.Values;
    }

    // Pending interaction management
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
}
