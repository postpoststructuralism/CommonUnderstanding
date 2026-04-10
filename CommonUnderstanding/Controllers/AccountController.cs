using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class AccountController : Controller
{
    private readonly AccountService _accounts;
    private readonly UserProfileStore _profileStore;
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AccountService accounts,
        UserProfileStore profileStore,
        BeliefDiscoveryOrchestrator orchestrator,
        ILogger<AccountController> logger)
    {
        _accounts = accounts;
        _profileStore = profileStore;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // ─── Login ───────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(returnUrl ?? Url.Action("Index", "Dashboard")!);

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "Please enter your username and password.");
            return View();
        }

        var account = await _accounts.ValidateCredentialsAsync(username, password);
        if (account is null)
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View();
        }

        await SignInAsync(account);
        EnsureProfileExists(account);

        _logger.LogInformation("User {Username} logged in", account.Username);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    // ─── Register ─────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string username, string displayName, string password, string confirmPassword)
    {
        if (password != confirmPassword)
        {
            ModelState.AddModelError("", "Passwords do not match.");
            return View();
        }

        var (account, error) = await _accounts.CreateAccountAsync(username, displayName, password);
        if (account is null)
        {
            ModelState.AddModelError("", error ?? "Registration failed.");
            return View();
        }

        // Create the UserProfile so discovery pipeline works immediately
        var profile = new UserProfile
        {
            Id   = account.Id,
            Name = account.DisplayName,
            Stage = DiscoveryStage.Initial
        };
        _profileStore.AddProfile(profile);

        await SignInAsync(account);
        _logger.LogInformation("New account registered: {Username} ({Id})", account.Username, account.Id);

        return RedirectToAction("Start", "Discovery");
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // ─── Access Denied ────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task SignInAsync(UserAccount account)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id),
            new(ClaimTypes.Name,           account.DisplayName),
            new("username",               account.Username),
        };
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }

    private void EnsureProfileExists(UserAccount account)
    {
        if (!_profileStore.ProfileExists(account.Id))
        {
            var profile = new UserProfile
            {
                Id   = account.Id,
                Name = account.DisplayName,
                Stage = DiscoveryStage.Initial
            };
            _profileStore.AddProfile(profile);
        }
    }
}
