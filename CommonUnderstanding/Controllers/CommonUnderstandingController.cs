using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Services;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Controllers;

public class CommonUnderstandingController : Controller
{
    private readonly CommonUnderstandingService _cuService;
    private readonly ILogger<CommonUnderstandingController> _logger;

    public CommonUnderstandingController(
        CommonUnderstandingService cuService,
        ILogger<CommonUnderstandingController> logger)
    {
        _cuService = cuService;
        _logger = logger;
    }

    // GET /CommonUnderstanding
    public async Task<IActionResult> Index(string? search, string? filter)
    {
        ViewBag.Query = search;
        ViewBag.Filter = filter;
        ViewBag.Stats = await _cuService.GetStatisticsAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var results = await _cuService.SearchAsync(search);
            ViewBag.IsSearch = true;
            return View(results);
        }

        var all = await _cuService.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(filter) && filter != "all")
        {
            if (filter == "gaps")
            {
                var gaps = all.Where(n => n.EvidenceCount == 0).ToList();
                ViewBag.Grouped = null;
                ViewBag.IsSearch = false;
                return View(gaps);
            }
            if (Enum.TryParse<PropositionStatus>(filter, ignoreCase: true, out var statusFilter))
            {
                var filtered = all.Where(n => n.Status == statusFilter).ToList();
                ViewBag.Grouped = null;
                ViewBag.IsSearch = false;
                return View(filtered);
            }
        }

        var grouped = await _cuService.GetGroupedByStatusAsync();
        ViewBag.Grouped = grouped;
        ViewBag.IsSearch = false;
        return View(all);
    }

    // GET /CommonUnderstanding/Node/{id}
    public async Task<IActionResult> Node(int id)
    {
        var node = await _cuService.GetWithEdgesAsync(id);
        if (node == null) return NotFound();
        return View(node);
    }
}
