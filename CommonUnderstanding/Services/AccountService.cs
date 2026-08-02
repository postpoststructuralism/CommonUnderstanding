using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Manages UserAccount persistence. Uses IServiceScopeFactory (singleton-safe pattern).
/// Password hashing via ASP.NET Core PasswordHasher — no plaintext stored.
/// </summary>
public class AccountService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PasswordHasher<UserAccount> _hasher = new();
    private readonly ILogger<AccountService> _logger;

    public AccountService(IServiceScopeFactory scopeFactory, ILogger<AccountService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<(UserAccount? Account, string? Error)> CreateAccountAsync(
        string username, string displayName, string password)
    {
        if (string.IsNullOrWhiteSpace(username))  return (null, "Username is required.");
        if (string.IsNullOrWhiteSpace(displayName)) return (null, "Display name is required.");
        if (string.IsNullOrWhiteSpace(password))  return (null, "Password is required.");
        if (password.Length < 8) return (null, "Password must be at least 8 characters.");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await db.UserAccounts.AnyAsync(a => a.Username == username.ToLower()))
            return (null, "That username is already taken.");

        var account = new UserAccount
        {
            Username    = username.ToLower().Trim(),
            DisplayName = displayName.Trim(),
        };
        account.PasswordHash = _hasher.HashPassword(account, password);

        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();
        _logger.LogInformation("Created account {Username} ({Id})", account.Username, account.Id);
        return (account, null);
    }

    public async Task<UserAccount?> ValidateCredentialsAsync(string username, string password)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var account = await db.UserAccounts
            .FirstOrDefaultAsync(a => a.Username == username.ToLower() && a.IsActive && !a.IsServiceAccount);
        if (account is null) return null;

        var result = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : account;
    }

    public async Task<UserAccount?> GetByIdAsync(string id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserAccounts.FindAsync(id);
    }

    public async Task<UserAccount?> GetByUsernameAsync(string username)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserAccounts.FirstOrDefaultAsync(a => a.Username == username.ToLower());
    }

    public async Task<List<UserAccount>> GetAllAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserAccounts.Where(a => a.IsActive).OrderBy(a => a.DisplayName).ToListAsync();
    }
}
