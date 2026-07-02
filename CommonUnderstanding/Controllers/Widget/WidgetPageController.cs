using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommonUnderstanding.Controllers.Widget;

/// <summary>
/// MVC controller for the publisher dashboard pages.
/// </summary>
[Authorize]
public class WidgetPageController : Controller
{
    public IActionResult Dashboard()
    {
        return View();
    }

    [Route("Widget/Dashboard/{siteId}")]
    public IActionResult SiteDashboard(Guid siteId)
    {
        return View("SiteDashboard", siteId);
    }

    public IActionResult Register()
    {
        return View();
    }

    [Route("Widget/Moderation/{siteId}")]
    public IActionResult Moderation(Guid siteId)
    {
        return View("Moderation", siteId);
    }

    [Route("Widget/Settings/{siteId}")]
    public IActionResult Settings(Guid siteId)
    {
        return View("Settings", siteId);
    }
}