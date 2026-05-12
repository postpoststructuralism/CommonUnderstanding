namespace CommonUnderstanding.Models;

/// <summary>
/// A manually-managed user account for login. The Id doubles as the ProfileId
/// used throughout the belief-discovery and convergence pipeline.
/// Designed for easy migration to ADFS WS-Federation: replace cookie auth scheme,
/// map ClaimTypes.NameIdentifier from the ADFS claim — all downstream code unchanged.
/// </summary>
public class UserAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
