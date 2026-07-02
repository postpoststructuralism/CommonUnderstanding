using System.Security.Claims;
using System.Text.Encodings.Web;
using CommonUnderstanding.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CommonUnderstanding.Authentication;

/// <summary>
/// API Key authentication handler for the embeddable widget.
/// Validates the X-Api-Key header against registered CommentSites.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDbContextFactory<ApplicationDbContext> contextFactory)
        : base(options, logger, encoder)
    {
        _contextFactory = contextFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var site = await db.CommentSites
            .FirstOrDefaultAsync(s => s.ApiKey == apiKey && s.IsActive);

        if (site == null)
        {
            return AuthenticateResult.Fail("Invalid or inactive API key");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, site.Id.ToString()),
            new Claim(ClaimTypes.Role, "widget_publisher"),
            new Claim("site_id", site.Id.ToString()),
            new Claim("owner_user_id", site.OwnerUserId),
            new Claim("plan_tier", site.PlanTier)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
}