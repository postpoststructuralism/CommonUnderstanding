using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class ConvergenceController : Controller
{
    private readonly ConvergenceMapService _convergenceMapService;
    private readonly ConvergenceExpansionService _expansionService;
    private readonly UserConnectionService _connectionService;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<ConvergenceController> _logger;

    private static readonly JsonSerializerOptions _json = new();

    public ConvergenceController(
        ConvergenceMapService convergenceMapService,
        ConvergenceExpansionService expansionService,
        UserConnectionService connectionService,
        UserProfileStore profileStore,
        ILogger<ConvergenceController> logger)
    {
        _convergenceMapService = convergenceMapService;
        _expansionService = expansionService;
        _connectionService = connectionService;
        _profileStore = profileStore;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Convergence
    //  Dashboard listing all convergence maps for the current user.
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var maps = await _convergenceMapService.GetMapsForUserAsync(userId);

        var vm = new ConvergenceDashboardViewModel
        {
            CurrentUserId = userId,
            Maps = maps.Select(m => BuildMapSummary(m, userId)).ToList()
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Convergence/{otherUserId}
    //  Shows or generates the map between current user and another user.
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Map(string otherUserId, bool regenerate = false)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        // Only allow connections to view each other's maps
        if (!await _connectionService.AreConnectedAsync(userId, otherUserId))
        {
            TempData["Error"] = "You must be connected with this user to view a convergence map.";
            return RedirectToAction(nameof(Index));
        }

        ConvergenceMap map;
        if (regenerate)
        {
            map = await _convergenceMapService.GenerateAsync(userId, otherUserId);
        }
        else
        {
            map = await _convergenceMapService.GetMapAsync(userId, otherUserId)
                  ?? await _convergenceMapService.GenerateAsync(userId, otherUserId);
        }

        var otherUser = _profileStore.GetProfile(otherUserId);
        var currentUser = _profileStore.GetProfile(userId);

        var vm = new ConvergenceMapViewModel
        {
            Map = map,
            CurrentUserId = userId,
            OtherUserId = otherUserId,
            CurrentUserName = currentUser?.Name ?? userId,
            OtherUserName = otherUser?.Name ?? otherUserId,
            ProfileOverlap = Deserialize<BeliefComparison>(map.ProfileOverlapJson),
            DivergencePoints = Deserialize<List<DivergenceDimension>>(map.DivergencePointsJson),
            ExpansionPathways = Deserialize<List<ExpansionPathway>>(map.ExpansionPathwaysJson),
            EvolutionHistory = Deserialize<List<ConvergenceSnapshot>>(map.EvolutionHistoryJson),
            SharedPropositionCount = Deserialize<List<int>>(map.SharedPropositionIdsJson).Count,
            DisputedPropositionCount = Deserialize<List<int>>(map.DisputedPropositionIdsJson).Count
        };

        return View(vm);
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

    private ConvergenceMapSummary BuildMapSummary(ConvergenceMap map, string currentUserId)
    {
        var otherId = map.User1Id == currentUserId ? map.User2Id : map.User1Id;
        var other = _profileStore.GetProfile(otherId);
        var history = Deserialize<List<ConvergenceSnapshot>>(map.EvolutionHistoryJson);
        return new ConvergenceMapSummary
        {
            Map = map,
            OtherUserId = otherId,
            OtherUserName = other?.Name ?? otherId,
            SharedCount = Deserialize<List<int>>(map.SharedPropositionIdsJson).Count,
            DisputedCount = Deserialize<List<int>>(map.DisputedPropositionIdsJson).Count,
            Trend = history.Count >= 2
                ? map.OverallConvergenceScore - history[^2].ConvergenceScore
                : 0
        };
    }

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
    public string CurrentUserId { get; set; } = string.Empty;
    public List<ConvergenceMapSummary> Maps { get; set; } = new();
}

public class ConvergenceMapSummary
{
    public ConvergenceMap Map { get; set; } = null!;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserName { get; set; } = string.Empty;
    public int SharedCount { get; set; }
    public int DisputedCount { get; set; }
    public double Trend { get; set; }  // positive = growing convergence
}

public class ConvergenceMapViewModel
{
    public ConvergenceMap Map { get; set; } = null!;
    public string CurrentUserId { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string CurrentUserName { get; set; } = string.Empty;
    public string OtherUserName { get; set; } = string.Empty;
    public BeliefComparison? ProfileOverlap { get; set; }
    public List<DivergenceDimension> DivergencePoints { get; set; } = new();
    public List<ExpansionPathway> ExpansionPathways { get; set; } = new();
    public List<ConvergenceSnapshot> EvolutionHistory { get; set; } = new();
    public int SharedPropositionCount { get; set; }
    public int DisputedPropositionCount { get; set; }
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
