using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace CommonUnderstanding.Controllers;

/// <summary>
/// The conversation shell — the new default UX for CommonUnderstanding.
/// The agent drives the conversation; the understanding graph is a toggleable overlay.
/// </summary>
public class ConversationController : Controller
{
    private readonly IFeatureManager _featureManager;

    public ConversationController(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    /// <summary>
    /// The main conversation view. This replaces Home/Index as the default landing page
    /// when the ConversationShell feature flag is enabled.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        ViewBag.IsGraphEnabled = await _featureManager.IsEnabledAsync("UnderstandingGraph");
        return View();
    }
}