using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class ConnectionsController : Controller
{
    private readonly UserConnectionService _connectionService;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<ConnectionsController> _logger;

    public ConnectionsController(
        UserConnectionService connectionService,
        UserProfileStore profileStore,
        ILogger<ConnectionsController> logger)
    {
        _connectionService = connectionService;
        _profileStore = profileStore;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Connections
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var connections    = await _connectionService.GetConnectionsForUserAsync(userId);
        var pending        = await _connectionService.GetPendingInvitesForUserAsync(userId);
        var sent           = await _connectionService.GetSentInvitesForUserAsync(userId);
        var discoverable   = await _connectionService.GetDiscoverableUsersAsync(userId);

        var vm = new ConnectionsIndexViewModel
        {
            CurrentUserId    = userId,
            CurrentUserName  = _profileStore.GetProfile(userId)?.Name ?? userId,
            Connections      = EnrichConnections(connections, userId),
            PendingInvites   = EnrichConnections(pending, userId),
            SentInvites      = EnrichConnections(sent, userId),
            DiscoverableUsers = discoverable
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Connections/Invite
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string recipientUserId, string? message)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            TempData["Error"] = "Please specify a user to connect with.";
            return RedirectToAction(nameof(Index));
        }

        var connection = await _connectionService.InitiateConnectionAsync(userId, recipientUserId, message);
        if (connection is null)
        {
            TempData["Error"] = "Could not send invite. The user may not exist.";
        }
        else
        {
            TempData["Success"] = "Connection invite sent.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Connections/Accept
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int connectionId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        // Verify the current user is the recipient
        var accepted = await _connectionService.AcceptConnectionAsync(connectionId);
        TempData[accepted ? "Success" : "Error"] = accepted ? "Connection accepted." : "Could not accept connection.";

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Connections/Decline
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(int connectionId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        var declined = await _connectionService.DeclineConnectionAsync(connectionId);
        TempData[declined ? "Success" : "Error"] = declined ? "Invite declined." : "Could not decline invite.";

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private List<ConnectionViewModel> EnrichConnections(List<UserConnection> connections, string currentUserId)
    {
        return connections.Select(c =>
        {
            var otherId = c.InitiatorUserId == currentUserId ? c.RecipientUserId : c.InitiatorUserId;
            var other = _profileStore.GetProfile(otherId);
            return new ConnectionViewModel
            {
                Connection = c,
                OtherUserId = otherId,
                OtherUserName = other?.Name ?? otherId,
                OtherUserStage = other?.Stage ?? DiscoveryStage.Initial
            };
        }).ToList();
    }
}

// ─────────────────────────────────────────────
//  View models
// ─────────────────────────────────────────────

public class ConnectionsIndexViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public string CurrentUserName { get; set; } = string.Empty;
    public List<ConnectionViewModel> Connections { get; set; } = new();
    public List<ConnectionViewModel> PendingInvites { get; set; } = new();
    public List<ConnectionViewModel> SentInvites { get; set; } = new();
    public List<UserProfile> DiscoverableUsers { get; set; } = new();
}

public class ConnectionViewModel
{
    public UserConnection Connection { get; set; } = null!;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserName { get; set; } = string.Empty;
    public DiscoveryStage OtherUserStage { get; set; }
}
