using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class DiscoveryController : Controller
{
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly ILogger<DiscoveryController> _logger;
    
    // In-memory storage for demo - replace with database
    private static readonly Dictionary<string, UserProfile> _profiles = new();
    private static readonly Dictionary<string, UserInteraction> _pendingInteractions = new();

    public DiscoveryController(
        BeliefDiscoveryOrchestrator orchestrator,
        ILogger<DiscoveryController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // GET: Discovery/Start
    public IActionResult Start()
    {
        return View();
    }

    // POST: Discovery/Start
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            ModelState.AddModelError("", "Please enter your name");
            return View();
        }

        try
        {
            // Create new user profile
            var profile = new UserProfile
            {
                Name = userName,
                Stage = DiscoveryStage.Initial
            };

            _profiles[profile.Id] = profile;

            // Generate first question
            var firstQuestion = await _orchestrator.StartDiscoveryAsync(profile);
            _pendingInteractions[profile.Id] = firstQuestion;

            // Store profile ID in session/cookie for tracking
            HttpContext.Response.Cookies.Append("ProfileId", profile.Id, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });

            return RedirectToAction(nameof(Question));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting discovery for {UserName}", userName);
            ModelState.AddModelError("", "Error starting discovery. Make sure Ollama is running.");
            return View();
        }
    }

    // GET: Discovery/Question
    public IActionResult Question()
    {
        var profileId = HttpContext.Request.Cookies["ProfileId"];
        if (string.IsNullOrEmpty(profileId) || !_profiles.ContainsKey(profileId))
        {
            return RedirectToAction(nameof(Start));
        }

        var profile = _profiles[profileId];
        var question = _pendingInteractions.GetValueOrDefault(profileId);

        if (question == null)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewBag.Profile = profile;
        ViewBag.ProgressPercent = CalculateProgress(profile);
        
        return View(question);
    }

    // POST: Discovery/Question
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitResponse(string response, double? numericValue)
    {
        var profileId = HttpContext.Request.Cookies["ProfileId"];
        if (string.IsNullOrEmpty(profileId) || !_profiles.ContainsKey(profileId))
        {
            return RedirectToAction(nameof(Start));
        }

        var profile = _profiles[profileId];
        var interaction = _pendingInteractions.GetValueOrDefault(profileId);

        if (interaction == null || string.IsNullOrWhiteSpace(response))
        {
            TempData["Error"] = "Please provide a response";
            return RedirectToAction(nameof(Question));
        }

        try
        {
            var responseStartTime = DateTime.UtcNow;

            // Fill in the response
            interaction.Response = new UserResponse
            {
                RawText = response,
                NumericValue = numericValue
            };
            interaction.ResponseTimeMs = (long)(DateTime.UtcNow - interaction.Timestamp).TotalMilliseconds;

            // Add to profile's interactions
            profile.Interactions.Add(interaction);

            // Process response and get next question
            var (updatedModel, nextQuestion) = await _orchestrator.ProcessResponseAndContinueAsync(
                profile, interaction);

            // Store next question
            _pendingInteractions[profileId] = nextQuestion;

            // Check if we should show profile
            if (profile.InteractionCount >= 5 && profile.InteractionCount % 10 == 0)
            {
                TempData["ShowProfilePrompt"] = true;
            }

            return RedirectToAction(nameof(Question));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing response for user {ProfileId}", profileId);
            TempData["Error"] = "Error processing your response. Make sure Ollama is running.";
            return RedirectToAction(nameof(Question));
        }
    }

    // GET: Discovery/Profile
    public IActionResult Profile()
    {
        var profileId = HttpContext.Request.Cookies["ProfileId"];
        if (string.IsNullOrEmpty(profileId) || !_profiles.ContainsKey(profileId))
        {
            return RedirectToAction(nameof(Start));
        }

        var profile = _profiles[profileId];
        return View(profile);
    }

    // GET: Discovery/History
    public IActionResult History()
    {
        var profileId = HttpContext.Request.Cookies["ProfileId"];
        if (string.IsNullOrEmpty(profileId) || !_profiles.ContainsKey(profileId))
        {
            return RedirectToAction(nameof(Start));
        }

        var profile = _profiles[profileId];
        return View(profile);
    }

    // GET: Discovery/Evolution
    public IActionResult Evolution()
    {
        var profileId = HttpContext.Request.Cookies["ProfileId"];
        if (string.IsNullOrEmpty(profileId) || !_profiles.ContainsKey(profileId))
        {
            return RedirectToAction(nameof(Start));
        }

        var profile = _profiles[profileId];
        
        // Show how the model has evolved over time
        var snapshots = new List<BeliefSnapshot>();
        if (profile.CurrentBeliefSnapshot != null)
        {
            snapshots.AddRange(profile.HistoricalSnapshots);
            snapshots.Add(profile.CurrentBeliefSnapshot);
        }

        ViewBag.Profile = profile;
        return View(snapshots);
    }

    // GET: Discovery/Compare
    public IActionResult Compare()
    {
        ViewBag.Profiles = _profiles.Values.Where(p => p.InteractionCount >= 10).ToList();
        return View();
    }

    // POST: Discovery/Compare
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Compare(string profile1Id, string profile2Id)
    {
        if (string.IsNullOrEmpty(profile1Id) || string.IsNullOrEmpty(profile2Id))
        {
            TempData["Error"] = "Please select two profiles to compare";
            return RedirectToAction(nameof(Compare));
        }

        var profile1 = _profiles.GetValueOrDefault(profile1Id);
        var profile2 = _profiles.GetValueOrDefault(profile2Id);

        if (profile1 == null || profile2 == null)
        {
            TempData["Error"] = "Invalid profiles selected";
            return RedirectToAction(nameof(Compare));
        }

        ViewBag.Profile1 = profile1;
        ViewBag.Profile2 = profile2;

        return View("CompareResult");
    }

    // GET: Discovery/AllProfiles
    public IActionResult AllProfiles()
    {
        var profiles = _profiles.Values
            .OrderByDescending(p => p.InteractionCount)
            .ToList();
        
        return View(profiles);
    }

    #region Helper Methods

    private int CalculateProgress(UserProfile profile)
    {
        // Progress based on interaction count and confidence
        var countProgress = Math.Min(profile.InteractionCount * 2, 50);
        var confidenceProgress = (int)((profile.CurrentBeliefSnapshot?.OverallConfidence ?? 0) * 50);
        return Math.Min(countProgress + confidenceProgress, 100);
    }

    #endregion
}
