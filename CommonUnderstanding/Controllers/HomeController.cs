using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UnderstandingQueryService _queryService;

    public HomeController(
        ILogger<HomeController> logger,
        UnderstandingQueryService queryService)
    {
        _logger = logger;
        _queryService = queryService;
    }

    public async Task<IActionResult> Index()
    {
        // Load lightweight stats for the map homepage.
        // The full map data, schemas, syntheses, etc. load via AJAX from
        // /api/understanding-graph/* endpoints — same as UnderstandingGraph/Index.
        var stats = await _queryService.GetQuickStatsAsync();
        ViewBag.Statistics = stats;
        ViewBag.IsHomePage = true;  // Hide sidebar when served as homepage
        return View("~/Views/UnderstandingGraph/Index.cshtml");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Components()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
