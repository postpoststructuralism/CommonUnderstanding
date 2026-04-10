using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class DiscoveryController : Controller
{
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly UserProfileStore _profileStore;
    private readonly BeliefSystemKnowledgeBase _knowledgeBase;
    private readonly ResponseProcessingQueue _responseQueue;
    private readonly QuestionPrefetchService _prefetchService;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(
        BeliefDiscoveryOrchestrator orchestrator,
        UserProfileStore profileStore,
        BeliefSystemKnowledgeBase knowledgeBase,
        ResponseProcessingQueue responseQueue,
        QuestionPrefetchService prefetchService,
        ILogger<DiscoveryController> logger)
    {
        _orchestrator = orchestrator;
        _profileStore = profileStore;
        _knowledgeBase = knowledgeBase;
        _responseQueue = responseQueue;
        _prefetchService = prefetchService;
        _logger = logger;
    }

    // GET: Discovery/Start
    public async Task<IActionResult> Start()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId)) return RedirectToAction("Login", "Account");

        if (_profileStore.ProfileExists(profileId))
        {
            // If a question is already queued, just continue; otherwise fall through to generate one.
            if (_profileStore.GetPendingInteraction(profileId) != null)
                return RedirectToAction(nameof(Question));

            var existingProfile = _profileStore.GetProfile(profileId)!;
            var resumeQuestion = await _orchestrator.StartDiscoveryAsync(existingProfile);
            _profileStore.SetPendingInteraction(profileId, resumeQuestion);
            return RedirectToAction(nameof(Question));
        }

        // Profile doesn't exist yet (returning user before profile was persisted) — create it
        var displayName = User.FindFirstValue(ClaimTypes.Name) ?? "User";
        var profile = new UserProfile { Id = profileId, Name = displayName, Stage = DiscoveryStage.Initial };
        _profileStore.AddProfile(profile);

        var firstQuestion = await _orchestrator.StartDiscoveryAsync(profile);
        _profileStore.SetPendingInteraction(profileId, firstQuestion);

        return RedirectToAction(nameof(Question));
    }

    // GET: Discovery/Question
    public async Task<IActionResult> Question()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId)) return RedirectToAction("Login", "Account");
        if (!_profileStore.ProfileExists(profileId)) return RedirectToAction(nameof(Start));

        var profile = _profileStore.GetProfile(profileId);
        var question = _profileStore.GetPendingInteraction(profileId);

        if (question == null)
        {
            // No pending question (e.g. after a server restart clears in-memory state).
            // Generate one now so the user isn't stuck in a Profile ↔ Question redirect loop.
            _logger.LogInformation("No pending question for {ProfileId}, generating one now", profileId);
            question = await _orchestrator.GetNextQuestionAsync(profile!);
            _profileStore.SetPendingInteraction(profileId, question);
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
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId)) return RedirectToAction("Login", "Account");
        if (!_profileStore.ProfileExists(profileId)) return RedirectToAction(nameof(Start));

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

        // QUEUE the response for background processing instead of blocking
        _responseQueue.QueueResponse(profileId, interaction);
        _logger.LogInformation("Response queued for user {ProfileId}, queue depth: {Depth}", 
            profileId, _responseQueue.GetQueueDepth());

        // IMMEDIATELY get next question from prefetch queue or generate one
        UserInteraction nextQuestion;
        if (profile.PrefetchedQuestions.TryDequeue(out var prefetchedQuestion))
        {
            // Mark question as asked
            var hash = ComputeQuestionHash(prefetchedQuestion);
            profile.AskedQuestionHashes.Add(hash);
            nextQuestion = prefetchedQuestion;
            _logger.LogInformation("Using prefetched question for user {ProfileId}, {Count} remaining", 
                profileId, profile.PrefetchedQuestions.Count);
        }
        else
        {
            // No prefetched questions available; generate exactly one question immediately.
            // Using GetNextQuestionAsync avoids the bulk Ollama calls of StartDiscoveryAsync.
            _logger.LogWarning("No prefetched questions available for user {ProfileId}, generating synchronously", profileId);
            nextQuestion = await _orchestrator.GetNextQuestionAsync(profile);
        }

        // NOTE: Prefetch is now triggered by ResponseProcessingQueue AFTER analysis completes
        // This ensures questions are generated based on updated belief state
        // See ResponseProcessingQueue.ProcessSingleResponse() and ProcessResponseBatch()

        // Store next question
        _profileStore.SetPendingInteraction(profileId, nextQuestion);

        // Check if we should show profile
        if (profile.InteractionCount >= 5 && profile.InteractionCount % 10 == 0)
        {
            TempData["ShowProfilePrompt"] = true;
        }

        return RedirectToAction(nameof(Question));
    }

    /// <summary>
    /// Compute hash for question to detect duplicates
    /// </summary>
    private string ComputeQuestionHash(UserInteraction interaction)
    {
        var content = interaction.Content.Question ?? "";
        if (interaction.Content.Options?.Any() == true)
        {
            content += "|" + string.Join("|", interaction.Content.Options);
        }
        if (!string.IsNullOrEmpty(interaction.Content.Context))
        {
            content += "|" + interaction.Content.Context;
        }
        return content.GetHashCode().ToString();
    }

    // GET: Discovery/Profile
    public IActionResult Profile()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
            return RedirectToAction(nameof(Start));

        var profile = _profileStore.GetProfile(profileId);
        if (profile == null)
            return RedirectToAction(nameof(Start));

        if (profile.CurrentBeliefSnapshot != null && profile.InteractionCount >= 5)
        {
            var universePosition = _knowledgeBase.CalculateUniversePosition(profile.CurrentBeliefSnapshot);
            ViewBag.UniversePosition = universePosition;
            ViewBag.AllSystems = _knowledgeBase.AllSystems.ToList();
        }
        return View(profile);
    }

    // GET: Discovery/History
    public IActionResult History()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
            return RedirectToAction(nameof(Start));
        return View(_profileStore.GetProfile(profileId));
    }

    // GET: Discovery/Evolution
    public IActionResult Evolution()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
            return RedirectToAction(nameof(Start));

        var profile = _profileStore.GetProfile(profileId);
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

    // GET: Discovery/CompareToCanonical
    public IActionResult CompareToCanonical()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
        {
            TempData["Error"] = "No active profile found. Please start discovery first.";
            return RedirectToAction(nameof(Start));
        }

        var profile = _profileStore.GetProfile(profileId);
        
        if (profile?.CurrentBeliefSnapshot == null || profile.InteractionCount < 5)
        {
            TempData["Error"] = "Please answer at least 5 questions before comparing your beliefs.";
            return RedirectToAction(nameof(Question));
        }

        // Get top matches
        var matches = _knowledgeBase.CompareUserToCanonicalSystems(profile.CurrentBeliefSnapshot, topN: 20);
        
        ViewBag.Profile = profile;
        ViewBag.Snapshot = profile.CurrentBeliefSnapshot;
        
        return View(matches);
    }

    // GET: Discovery/Debug (for troubleshooting)
    public IActionResult Debug()
    {
        var profileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(profileId) || !_profileStore.ProfileExists(profileId))
            return Json(new { error = "No active profile" });

        var profile = _profileStore.GetProfile(profileId);
        
        return Json(new
        {
            profileId = profile.Id,
            name = profile.Name,
            stage = profile.Stage.ToString(),
            interactionCount = profile.InteractionCount,
            prefetchedQuestionsCount = profile.PrefetchedQuestions.Count,
            askedQuestionHashes = profile.AskedQuestionHashes.Count,
            confidence = profile.CurrentBeliefSnapshot?.OverallConfidence ?? 0,
            entropy = profile.CurrentBeliefSnapshot?.Statistics.Entropy ?? 0,
            pendingResponseQueue = _responseQueue.GetPendingCountForUser(profileId),
            prefetchedQuestions = profile.PrefetchedQuestions.Select(q => new
            {
                type = q.Type.ToString(),
                question = q.Content.Question.Length > 100 
                    ? q.Content.Question.Substring(0, 97) + "..." 
                    : q.Content.Question
            }).ToList(),
            lastInteraction = profile.Interactions.LastOrDefault()?.Content.Question
        });
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
