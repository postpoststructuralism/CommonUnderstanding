using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using CommonUnderstanding.Services.Social;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class ConvergenceController : Controller
{
    private readonly UserAgreementService _userAgreementService;
    private readonly ConvergenceMapService _convergenceMapService;
    private readonly ConvergenceExpansionService _expansionService;
    private readonly UserConnectionService _connectionService;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<ConvergenceController> _logger;

    private static readonly JsonSerializerOptions _json = new();

    public ConvergenceController(
        UserAgreementService userAgreementService,
        ConvergenceMapService convergenceMapService,
        ConvergenceExpansionService expansionService,
        UserConnectionService connectionService,
        UserProfileStore profileStore,
        ILogger<ConvergenceController> logger)
    {
        _userAgreementService = userAgreementService;
        _convergenceMapService = convergenceMapService;
        _expansionService = expansionService;
        _connectionService = connectionService;
        _profileStore = profileStore;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Convergence
    //  Ranks active users by shared decisive votes on public contributions.
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var vm = new ConvergenceDashboardViewModel
        {
            Agreements = await _userAgreementService.GetRankingsAsync(userId, cancellationToken)
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Convergence/{otherUserId}
    //  Shows the public contributions where two users agree or disagree.
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Map(string otherUserId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var detail = await _userAgreementService.GetDetailAsync(userId, otherUserId, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Convergence/Expand/{otherUserId}
    //  Shows the expansion session interface for the current user.
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Expand(string otherUserId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        if (!await _connectionService.AreConnectedAsync(userId, otherUserId))
        {
            TempData["Error"] = "You must be connected with this user to start an expansion session.";
            return RedirectToAction(nameof(Index));
        }

        var map = await _convergenceMapService.GetMapAsync(userId, otherUserId)
                  ?? await _convergenceMapService.GenerateAsync(userId, otherUserId);

        var (q1, q2) = _expansionService.GetNextQuestionPair(map);

        // Determine which question belongs to the current user
        var myQuestion = map.User1Id == userId ? q1 : q2;

        if (myQuestion is null)
        {
            TempData["Info"] = "No expansion questions are available for this map yet. Try regenerating it.";
            return RedirectToAction(nameof(Map), new { otherUserId });
        }

        var otherUser = _profileStore.GetProfile(otherUserId);
        var vm = new ExpansionSessionViewModel
        {
            MapId = map.Id,
            CurrentUserId = userId,
            OtherUserId = otherUserId,
            OtherUserName = otherUser?.Name ?? otherUserId,
            CurrentConvergenceScore = map.OverallConvergenceScore,
            Question = myQuestion,
            Pathways = Deserialize<List<ExpansionPathway>>(map.ExpansionPathwaysJson)
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Convergence/SubmitExpansionResponse
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitExpansionResponse(
        int mapId,
        string otherUserId,
        string questionText,
        string responseText)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            TempData["Error"] = "Please provide a response before submitting.";
            return RedirectToAction(nameof(Expand), new { otherUserId });
        }

        var interaction = new UserInteraction
        {
            UserId = userId,
            Content = new InteractionContent
            {
                Question = questionText,
                Context = "Convergence expansion session",
                Format = InteractionFormat.OpenText
            },
            Response = new UserResponse { RawText = responseText },
            Type = InteractionType.OpenEndedQuestion,
            TargetedDimensions = new() { "convergence_expansion" }
        };

        var updatedMap = await _expansionService.ProcessResponseAsync(mapId, userId, interaction);

        TempData["Success"] = $"Response submitted. Updated convergence score: {updatedMap.OverallConvergenceScore:F1}/100";
        return RedirectToAction(nameof(Map), new { otherUserId });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Convergence/Compare — direct comparison without requiring connection
    //  (for internal/admin use; validates with query params)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Compare(string user1, string user2)
    {
        if (string.IsNullOrWhiteSpace(user1) || string.IsNullOrWhiteSpace(user2))
            return BadRequest("Both user1 and user2 query parameters are required.");

        try
        {
            var map = await _convergenceMapService.GenerateAsync(user1, user2);
            return RedirectToAction(nameof(Map), new { otherUserId = user2 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compare endpoint failed for {U1} ↔ {U2}", user1, user2);
            return BadRequest(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static T Deserialize<T>(string json) where T : new()
    {
        try { return JsonSerializer.Deserialize<T>(json) ?? new T(); }
        catch { return new T(); }
    }
}

// ─────────────────────────────────────────────
//  View models
// ─────────────────────────────────────────────

public class ConvergenceDashboardViewModel
{
    public IReadOnlyList<UserAgreementSummary> Agreements { get; set; } = [];
}

public class ExpansionSessionViewModel
{
    public int MapId { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserName { get; set; } = string.Empty;
    public double CurrentConvergenceScore { get; set; }
    public UserInteraction Question { get; set; } = null!;
    public List<ExpansionPathway> Pathways { get; set; } = new();
}
