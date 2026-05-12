# Account System Plan

## Problem

The current identity system is fully anonymous — a random GUID is placed in a `ProfileId` cookie on first visit to `/Discovery/Start`. There is no login, no registration, no way to see your own user ID, and no accounts. This makes the multi-user convergence feature (connections, convergence maps, collaborative sessions) untestable because users have no stable, known identity.

## Goal

A simple, manually-managed account system that:
- Enables reliable testing of multi-user features
- Keeps the codebase on a clean migration path to ADFS WS-Federation

---

## Architecture

Use **ASP.NET Core Cookie Authentication** with claims-based identity from day one. When ADFS migration happens, the authentication scheme is swapped — all downstream code continues reading the same `ClaimTypes.NameIdentifier` claim unchanged.

The `UserAccount.Id` (a GUID) becomes the `ProfileId`. The two systems are unified — same ID used everywhere.

---

## New / Modified Components

### 1. `UserAccount` model (new)

```csharp
public class UserAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;       // unique login name
    public string DisplayName { get; set; } = string.Empty;    // shown to other users
    public string PasswordHash { get; set; } = string.Empty;   // ASP.NET PasswordHasher
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

Stored in a new `UserAccounts` SQLite table. Added to `ApplicationDbContext`.

### 2. `AccountService` (new singleton)

Thin service wrapping account CRUD:
- `CreateAccountAsync(username, displayName, password)` → `UserAccount`
- `ValidateCredentialsAsync(username, password)` → `UserAccount?`
- `GetAccountByIdAsync(id)` → `UserAccount?`
- `GetAccountByUsernameAsync(username)` → `UserAccount?`
- `GetAllAccountsAsync()` → `IEnumerable<UserAccount>`
- Uses `PasswordHasher<UserAccount>` — no plaintext passwords stored

### 3. `AccountController` (new)

| Action | Description |
|---|---|
| `GET /Account/Login` | Login form |
| `POST /Account/Login` | Validate → `SignInAsync()` with `ClaimTypes.NameIdentifier = account.Id` and `ClaimTypes.Name = account.DisplayName` |
| `GET /Account/Register` | Registration form |
| `POST /Account/Register` | Create account → auto-create `UserProfile` with same ID → sign in → redirect to Discovery |
| `POST /Account/Logout` | `SignOutAsync()` → redirect to Login |
| `GET /Account/AccessDenied` | Simple 403 page |

On successful login/register, a `UserProfile` is created in `UserProfileStore` (if not already present) so the discovery pipeline works immediately.

### 4. `Program.cs` changes

```csharp
// Service registration
builder.Services.AddScoped<AccountService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationSchemeName)
    .AddCookie(options => {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

// Middleware (order matters — before UseAuthorization)
app.UseAuthentication();
app.UseAuthorization();
```

DB schema: `CREATE TABLE IF NOT EXISTS UserAccounts` added to the startup schema block.

### 5. Identity reading — unified across all controllers

Replace all `GetCurrentUserId()` / direct cookie reads with:

```csharp
User.FindFirstValue(ClaimTypes.NameIdentifier)
```

This is the exact same claim ADFS WS-Fed will set — zero migration effort.

Remove `DiscoveryController`'s auto-profile-creation logic. Profile creation moves to `AccountController.Register`.

### 6. `_Layout.cshtml` — identity in nav bar

Right side of nav bar gains:
- Logged-in: `DisplayName` with a dropdown → "My Profile ID" (copyable GUID for sharing), "Logout"
- Logged-out: "Login" + "Register" links

The copyable profile ID solves the "how do I know my ID" problem for manual connection testing.

### 7. `[Authorize]` attributes

Added to:
- `DiscoveryController`
- `ConnectionsController`
- `ConvergenceController`
- `SharingController`
- `CollaborativeSessionController`

Left public (no `[Authorize]`):
- `HomeController`, `AccountController`, `ExploreController`, `ReferenceController`, `AIStatusController`

---

## DB Schema (added to startup)

```sql
CREATE TABLE IF NOT EXISTS UserAccounts (
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL DEFAULT '',
    PasswordHash TEXT NOT NULL DEFAULT '',
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    IsActive INTEGER NOT NULL DEFAULT 1
)
```

---

## Migration Path to ADFS WS-Federation

When ready:

1. Add `Microsoft.AspNetCore.Authentication.WsFederation` NuGet package
2. Replace `.AddCookie()` with `.AddWsFederation(options => { options.MetadataAddress = "..."; ... })`
3. Map `ClaimTypes.NameIdentifier` from the ADFS `upn` or `nameidentifier` claim
4. Add auto-provisioning in a `ClaimsTransformation` or in `AccountController.ExternalCallback`: on first ADFS login, create `UserAccount` + `UserProfile` if not found
5. Remove `Register` page
6. All downstream code (controllers, services) — **zero changes required**

---

## File Summary

| File | Action |
|---|---|
| `Models/AccountModels.cs` | **Create** — `UserAccount` |
| `Data/ApplicationDbContext.cs` | **Modify** — add `DbSet<UserAccount>` |
| `Program.cs` | **Modify** — auth scheme + `UseAuthentication()` + schema + `AccountService` registration |
| `Services/AccountService.cs` | **Create** |
| `Controllers/AccountController.cs` | **Create** |
| `Views/Account/Login.cshtml` | **Create** |
| `Views/Account/Register.cshtml` | **Create** |
| `Views/Account/AccessDenied.cshtml` | **Create** |
| `Views/Shared/_Layout.cshtml` | **Modify** — identity nav |
| `Controllers/DiscoveryController.cs` | **Modify** — remove auto-profile-creation, use claims |
| `Controllers/ConnectionsController.cs` | **Modify** — use `User.FindFirstValue(...)` |
| `Controllers/SharingController.cs` | **Modify** — use `User.FindFirstValue(...)` |
| `Controllers/ConvergenceController.cs` | **Modify** — use `User.FindFirstValue(...)` |
| `Controllers/CollaborativeSessionController.cs` | **Modify** — use `User.FindFirstValue(...)` |
