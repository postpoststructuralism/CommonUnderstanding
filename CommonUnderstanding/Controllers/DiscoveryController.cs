using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class DiscoveryController : Controller
{
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(
        BeliefDiscoveryOrchestrator orchestrator,
        UserProfileStore profileStore,
        ILogger<DiscoveryController> logger)
    {
        _orchestrator = orchestrator;
        _profileStore = profileStore;
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

        // Create new user profile
        var profile = new UserProfile
        {
            Name = userName,
            Stage = DiscoveryStage.Initial
        };

        _profileStore.AddProfile(profile);

        // Generate first question - exceptions will bubble up naturally
        var firstQuestion = await _orchestrator.StartDiscoveryAsync(profile);
        _profileStore.SetPendingInteraction(profile.Id, firstQuestion);

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

    // GET: Discovery/Question
    public IActionResult Question()
    {
        string? profileId = null;
        
        try
        {
            profileId = HttpContext.Request.Cookies["ProfileId"];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading ProfileId cookie, clearing it");
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }
        
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
        {
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }

        var profile = _profileStore.GetProfile(profileId);
        var question = _profileStore.GetPendingInteraction(profileId);

        if (question == null)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewBag.Profile = profile;
        ViewBag.ProgressPercent = CalculateProgress(profile!);
        
        return View(question);
    }

    // POST: Discovery/Question
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitResponse(string? response, string? selectedOption, string? responseText, double? numericValue)
    {
        string? profileId = null;
        
        try
        {
            profileId = HttpContext.Request.Cookies["ProfileId"];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading ProfileId cookie, clearing it");
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }
        
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
        {
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }

        var profile = _profileStore.GetProfile(profileId);
        var interaction = _profileStore.GetPendingInteraction(profileId);

        if (profile == null || interaction == null)
        {
            TempData["Error"] = "Session expired. Please start over.";
            return RedirectToAction(nameof(Start));
        }

        // Determine the actual response based on question format
        string actualResponse = response ?? selectedOption ?? "";
        
        // For scale questions, use the numeric value as the response
        if (interaction.Content.Format == InteractionFormat.Scale && numericValue.HasValue)
        {
            actualResponse = numericValue.Value.ToString();
        }
        
        // Validate we have some kind of response
        if (string.IsNullOrWhiteSpace(actualResponse))
        {
            TempData["Error"] = "Please provide a response";
            return RedirectToAction(nameof(Question));
        }

        var responseStartTime = DateTime.UtcNow;

        // Fill in the response - combine selected option with any additional text
        var fullResponseText = actualResponse;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            fullResponseText += "\n\nAdditional context: " + responseText;
        }

        interaction.Response = new UserResponse
        {
            RawText = fullResponseText,
            NumericValue = numericValue,
            SelectedOptions = !string.IsNullOrWhiteSpace(selectedOption) 
                ? new List<string> { selectedOption } 
                : null
        };
        interaction.ResponseTimeMs = (long)(DateTime.UtcNow - interaction.Timestamp).TotalMilliseconds;

        // Add to profile's interactions
        profile.Interactions.Add(interaction);

        // Process response and get next question - exceptions will bubble up naturally
        var (updatedModel, nextQuestion) = await _orchestrator.ProcessResponseAndContinueAsync(
            profile, interaction);

        // Store next question
        _profileStore.SetPendingInteraction(profileId, nextQuestion);

        // Check if we should show profile
        if (profile.InteractionCount >= 5 && profile.InteractionCount % 10 == 0)
        {
            TempData["ShowProfilePrompt"] = true;
        }

        return RedirectToAction(nameof(Question));
    }

    // GET: Discovery/Profile
    public IActionResult Profile()
    {
        string? profileId = null;
        
        try
        {
            profileId = HttpContext.Request.Cookies["ProfileId"];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading ProfileId cookie, clearing it");
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }
        
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
        {
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }

        var profile = _profileStore.GetProfile(profileId);
        return View(profile);
    }

    // GET: Discovery/History
    public IActionResult History()
    {
        string? profileId = null;
        
        try
        {
            profileId = HttpContext.Request.Cookies["ProfileId"];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading ProfileId cookie, clearing it");
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }
        
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
        {
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }

        var profile = _profileStore.GetProfile(profileId);
        return View(profile);
    }

    // GET: Discovery/Evolution
    public IActionResult Evolution()
    {
        string? profileId = null;
        
        try
        {
            profileId = HttpContext.Request.Cookies["ProfileId"];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading ProfileId cookie, clearing it");
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }
        
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
        {
            HttpContext.Response.Cookies.Delete("ProfileId");
            return RedirectToAction(nameof(Start));
        }

        var profile = _profileStore.GetProfile(profileId);
        
        // Show how the model has evolved over time
        var snapshots = new List<BeliefSnapshot>();
        if (profile!.CurrentBeliefSnapshot != null)
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
        ViewBag.Profiles = _profileStore.GetAllProfiles().Where(p => p.InteractionCount >= 10).ToList();
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

        var profile1 = _profileStore.GetProfile(profile1Id);
        var profile2 = _profileStore.GetProfile(profile2Id);

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
        var profiles = _profileStore.GetAllProfiles()
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
