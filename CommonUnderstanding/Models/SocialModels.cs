using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models;

// ─────────────────────────────────────────────
//  Enumerations
// ─────────────────────────────────────────────

public enum ConnectionStatus
{
    Pending,
    Active,
    Declined
}

public enum SharedItemType
{
    Argument,
    Analysis,
    EmergentReport,
    ConvergenceMap
}

public enum ItemVisibility
{
    Private,
    Connections,
    Public
}

// ─────────────────────────────────────────────
//  UserConnection — social graph edge
// ─────────────────────────────────────────────

/// <summary>
/// A directed connection request / accepted relationship between two users.
/// </summary>
public class UserConnection
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string InitiatorUserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RecipientUserId { get; set; } = string.Empty;

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;

    [MaxLength(500)]
    public string? InitiatorMessage { get; set; }

    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}

// ─────────────────────────────────────────────
//  SharedItem — content routed between users
// ─────────────────────────────────────────────

/// <summary>
/// Records that a user has shared a piece of content with one or more others.
/// </summary>
public class SharedItem
{
    public int Id { get; set; }

    public SharedItemType ItemType { get; set; }

    /// <summary>Primary key of the referenced entity (ArgumentId, ConvergenceMapId, etc.).</summary>
    [MaxLength(200)]
    public string ItemReferenceId { get; set; } = string.Empty;

    /// <summary>Display title derived at share time (cached to avoid re-loading).</summary>
    [MaxLength(300)]
    public string ItemTitle { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string SharedByUserId { get; set; } = string.Empty;

    /// <summary>JSON array of user IDs this was shared with.</summary>
    public string SharedWithUserIdsJson { get; set; } = "[]";

    public ItemVisibility Visibility { get; set; } = ItemVisibility.Connections;

    [MaxLength(1000)]
    public string? Message { get; set; }

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    /// <summary>JSON array of SharedItemReaction records.</summary>
    public string ReactionsJson { get; set; } = "[]";
}

/// <summary>
/// A user's reaction to a shared item.
/// </summary>
public class SharedItemReaction
{
    public string UserId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;   // e.g. "👍", "🤔", "💡"
    public string? Comment { get; set; }
    public DateTime ReactedAt { get; set; } = DateTime.UtcNow;
}
