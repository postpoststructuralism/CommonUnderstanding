using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class CollaborativeSessionController : Controller
{
    private readonly CollaborativeSessionService _sessionService;
    private readonly UserConnectionService _connectionService;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<CollaborativeSessionController> _logger;

    private static readonly JsonSerializerOptions _json = new();

    public CollaborativeSessionController(
        CollaborativeSessionService sessionService,
        UserConnectionService connectionService,
        UserProfileStore profileStore,
        ILogger<CollaborativeSessionController> logger)
    {
        _sessionService = sessionService;
        _connectionService = connectionService;
        _profileStore = profileStore;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /CollaborativeSession
    // ─────────────────────────────────────────────────────────────────────────

    public IActionResult Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        return View();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /CollaborativeSession/Create
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var connectedIds  = await _connectionService.GetConnectedUserIdsAsync(userId);
        var connectedUsers = connectedIds
            .Select(id => _profileStore.GetProfile(id))
            .Where(p => p is not null)
            .Cast<UserProfile>()
            .ToList();

        ViewBag.ConnectedUsers = connectedUsers;
        return View();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /CollaborativeSession/Create
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description, string[] invitedUserIds)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError("title", "Title is required.");
            return View();
        }

        var session = await _sessionService.CreateSessionAsync(userId, invitedUserIds, title, description);
        TempData["Success"] = "Collaborative session created.";

        return RedirectToAction(nameof(Details), new { id = session.Id });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /CollaborativeSession/Details/{id}
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var session = await _sessionService.GetSessionAsync(id);
        if (session is null) return NotFound();

        var participants = (JsonSerializer.Deserialize<List<string>>(session.ParticipantIdsJson) ?? new())
            .Select(uid => _profileStore.GetProfile(uid))
            .Where(p => p is not null)
            .Cast<UserProfile>()
            .ToList();

        var contributions = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(
            session.ContributedArgumentIdsJson) ?? new();

        var vm = new CollaborativeSessionViewModel
        {
            Session = session,
            CurrentUserId = userId,
            Participants = participants,
            Contributions = contributions,
            MergedNodeCount = (JsonSerializer.Deserialize<List<int>>(session.MergedNodeIdsJson) ?? new()).Count,
            IsParticipant = participants.Any(p => p.Id == userId)
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /CollaborativeSession/Contribute
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contribute(int sessionId, int[] argumentIds)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        if (argumentIds.Length == 0)
        {
            TempData["Error"] = "Please select at least one argument to contribute.";
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        await _sessionService.ContributeArgumentsAsync(sessionId, userId, argumentIds);
        TempData["Success"] = $"Added {argumentIds.Length} argument(s) to the session.";

        return RedirectToAction(nameof(Details), new { id = sessionId });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /CollaborativeSession/Analyze
    //  Triggers the full joint analysis pipeline.
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(int sessionId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        try
        {
            var session = await _sessionService.RunJointAnalysisAsync(sessionId);
            TempData["Success"] = "Joint analysis complete. Results are now available below.";
            return RedirectToAction(nameof(Results), new { id = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Joint analysis failed for session {Id}", sessionId);
            TempData["Error"] = $"Analysis failed: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /CollaborativeSession/Results/{id}
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Results(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var session = await _sessionService.GetSessionAsync(id);
        if (session is null) return NotFound();

        EmergentConclusionsReport? report = null;
        if (session.ConsolidatedReportJson is not null)
        {
            try { report = JsonSerializer.Deserialize<EmergentConclusionsReport>(session.ConsolidatedReportJson); }
            catch { /* malformed JSON — leave null */ }
        }

        var vm = new CollaborativeSessionResultsViewModel
        {
            Session = session,
            CurrentUserId = userId,
            Report = report,
            HasConvergenceMap = session.JointConvergenceMapId.HasValue
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}

// ─────────────────────────────────────────────
//  View models
// ─────────────────────────────────────────────

public class CollaborativeSessionViewModel
{
    public CollaborativeSession Session { get; set; } = null!;
    public string CurrentUserId { get; set; } = string.Empty;
    public List<UserProfile> Participants { get; set; } = new();
    public Dictionary<string, List<int>> Contributions { get; set; } = new();
    public int MergedNodeCount { get; set; }
    public bool IsParticipant { get; set; }
}

public class CollaborativeSessionResultsViewModel
{
    public CollaborativeSession Session { get; set; } = null!;
    public string CurrentUserId { get; set; } = string.Empty;
    public EmergentConclusionsReport? Report { get; set; }
    public bool HasConvergenceMap { get; set; }
}
