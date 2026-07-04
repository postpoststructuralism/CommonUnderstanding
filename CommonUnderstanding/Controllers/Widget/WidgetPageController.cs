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
        return View("~/Views/Widget/Dashboard.cshtml");
    }

    [Route("Widget/Dashboard/{siteId}")]
    public IActionResult SiteDashboard(Guid siteId)
    {
        return View("~/Views/Widget/SiteDashboard.cshtml", siteId);
    }

    public IActionResult Register()
    {
        return View("~/Views/Widget/Register.cshtml");
    }

    [Route("Widget/Moderation/{siteId}")]
    public IActionResult Moderation(Guid siteId)
    {
        return View("~/Views/Widget/Moderation.cshtml", siteId);
    }

    [Route("Widget/Settings/{siteId}")]
    public IActionResult Settings(Guid siteId)
    {
        return View("~/Views/Widget/Settings.cshtml", siteId);
    }
}