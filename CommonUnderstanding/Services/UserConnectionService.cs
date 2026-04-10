using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Manages the social graph of user connections: initiate, accept, decline, and query.
/// Enforces privacy — profiles are exposed only to accepted connections.
/// </summary>
public class UserConnectionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<UserConnectionService> _logger;

    public UserConnectionService(
        ApplicationDbContext db,
        UserProfileStore profileStore,
        ILogger<UserConnectionService> logger)
    {
        _db = db;
        _profileStore = profileStore;
        _logger = logger;
    }

    // ── Initiate ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Pending connection from initiator to recipient.
    /// Returns null if a connection already exists in either direction.
    /// </summary>
    public async Task<UserConnection?> InitiateConnectionAsync(
        string initiatorUserId,
        string recipientUserId,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        // Guard: both users must exist
        if (!_profileStore.ProfileExists(initiatorUserId) || !_profileStore.ProfileExists(recipientUserId))
        {
            _logger.LogWarning("InitiateConnection: one or both users not found ({A}, {B})", initiatorUserId, recipientUserId);
            return null;
        }

        // Guard: no duplicate connection exists
        var existing = await _db.UserConnections
            .Where(c =>
                (c.InitiatorUserId == initiatorUserId && c.RecipientUserId == recipientUserId) ||
                (c.InitiatorUserId == recipientUserId && c.RecipientUserId == initiatorUserId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation("Connection already exists between {A} and {B} (status: {S})",
                initiatorUserId, recipientUserId, existing.Status);
            return existing;
        }

        var connection = new UserConnection
        {
            InitiatorUserId = initiatorUserId,
            RecipientUserId = recipientUserId,
            Status = ConnectionStatus.Pending,
            InitiatorMessage = message
        };

        _db.UserConnections.Add(connection);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Connection initiated: {A} → {B}", initiatorUserId, recipientUserId);
        return connection;
    }

    // ── Respond ───────────────────────────────────────────────────────────────

    public async Task<bool> AcceptConnectionAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.UserConnections.FindAsync([connectionId], cancellationToken);
        if (connection is null || connection.Status != ConnectionStatus.Pending) return false;

        connection.Status = ConnectionStatus.Active;
        connection.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Connection {Id} accepted", connectionId);
        return true;
    }

    public async Task<bool> DeclineConnectionAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.UserConnections.FindAsync([connectionId], cancellationToken);
        if (connection is null || connection.Status != ConnectionStatus.Pending) return false;

        connection.Status = ConnectionStatus.Declined;
        connection.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Connection {Id} declined", connectionId);
        return true;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all Active connections for a given user.
    /// </summary>
    public async Task<List<UserConnection>> GetConnectionsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserConnections
            .Where(c => (c.InitiatorUserId == userId || c.RecipientUserId == userId)
                        && c.Status == ConnectionStatus.Active)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all Pending invites received by userId.
    /// </summary>
    public async Task<List<UserConnection>> GetPendingInvitesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserConnections
            .Where(c => c.RecipientUserId == userId && c.Status == ConnectionStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all Pending invites sent by userId.
    /// </summary>
    public async Task<List<UserConnection>> GetSentInvitesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserConnections
            .Where(c => c.InitiatorUserId == userId && c.Status == ConnectionStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns true if the two users have an Active connection.
    /// </summary>
    public async Task<bool> AreConnectedAsync(
        string userId1,
        string userId2,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserConnections
            .AnyAsync(c =>
                ((c.InitiatorUserId == userId1 && c.RecipientUserId == userId2) ||
                 (c.InitiatorUserId == userId2 && c.RecipientUserId == userId1))
                && c.Status == ConnectionStatus.Active,
                cancellationToken);
    }

    /// <summary>
    /// Returns the IDs of all users that userId is actively connected to.
    /// </summary>
    public async Task<List<string>> GetConnectedUserIdsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var connections = await GetConnectionsForUserAsync(userId, cancellationToken);
        return connections.Select(c =>
            c.InitiatorUserId == userId ? c.RecipientUserId : c.InitiatorUserId
        ).ToList();
    }

    /// <summary>
    /// Returns all registered users that userId is NOT yet connected to (for discovery).
    /// </summary>
    public async Task<List<UserProfile>> GetDiscoverableUsersAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var connectedIds = await GetConnectedUserIdsAsync(userId, cancellationToken);
        var pendingIds = await _db.UserConnections
            .Where(c => (c.InitiatorUserId == userId || c.RecipientUserId == userId)
                        && c.Status == ConnectionStatus.Pending)
            .Select(c => c.InitiatorUserId == userId ? c.RecipientUserId : c.InitiatorUserId)
            .ToListAsync(cancellationToken);

        var excludeIds = new HashSet<string>(connectedIds.Concat(pendingIds)) { userId };

        return _profileStore.GetAllProfiles()
            .Where(p => !excludeIds.Contains(p.Id))
            .ToList();
    }
}
