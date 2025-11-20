using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class ExploreController : Controller
{
    private readonly BeliefSystemKnowledgeBase _knowledgeBase;
    private readonly ILogger<ExploreController> _logger;

    public ExploreController(
        BeliefSystemKnowledgeBase knowledgeBase,
        ILogger<ExploreController> logger)
    {
        _knowledgeBase = knowledgeBase;
        _logger = logger;
    }

    // GET: Explore/Index
    public IActionResult Index(string? category = null, string? culture = null, string? search = null)
    {
        var allSystems = _knowledgeBase.AllSystems.ToList();

        // Apply filters
        if (!string.IsNullOrEmpty(category))
        {
            allSystems = allSystems.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(culture))
        {
            allSystems = allSystems.Where(s => s.Culture.Contains(culture, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(search))
        {
            allSystems = allSystems.Where(s => 
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.CorePrinciples.Any(p => p.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Get unique categories and cultures for filter dropdowns
        ViewBag.Categories = _knowledgeBase.AllSystems
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        ViewBag.Cultures = _knowledgeBase.AllSystems
            .SelectMany(s => s.Culture.Split('/'))
            .Select(c => c.Trim())
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        ViewBag.SelectedCategory = category;
        ViewBag.SelectedCulture = culture;
        ViewBag.SearchTerm = search;

        return View(allSystems);
    }

    // GET: Explore/System/{slug}
    [Route("Explore/System/{slug}")]
    public IActionResult System(string slug)
    {
        var system = _knowledgeBase.GetBySlug(Uri.UnescapeDataString(slug));
        // Fallback for legacy URLs: if not found by slug, try by name
        if (system == null)
        {
            system = _knowledgeBase.GetByName(Uri.UnescapeDataString(slug));
        }
        
        if (system == null)
        {
            return NotFound();
        }

        // Find related systems (same category or culture)
        var relatedSystems = _knowledgeBase.AllSystems
            .Where(s => s.Id != system.Id && (
                s.Category == system.Category || 
                s.Culture.Split('/').Any(c => system.Culture.Contains(c))
            ))
            .Take(6)
            .ToList();

        ViewBag.RelatedSystems = relatedSystems;

        return View(system);
    }

    // Legacy route: support name-based URLs and redirect to slug-based canonical route
    [Route("Explore/SystemByName/{name}")]
    public IActionResult SystemByName(string name)
    {
        var system = _knowledgeBase.GetByName(Uri.UnescapeDataString(name));
        if (system == null) return NotFound();
        return RedirectToActionPermanent(nameof(System), new { slug = system.Slug });
    }

    // GET: Explore/Compare
    public IActionResult Compare(string? system1 = null, string? system2 = null)
    {
        ViewBag.AllSystems = _knowledgeBase.AllSystems
            .OrderBy(s => s.Name)
            .ToList();

        if (string.IsNullOrEmpty(system1) || string.IsNullOrEmpty(system2))
        {
            return View((BeliefSystemComparison?)null);
        }

        // Try to resolve by slug first, then by name
        var s1 = _knowledgeBase.GetBySlug(Uri.UnescapeDataString(system1)) ?? _knowledgeBase.GetByName(Uri.UnescapeDataString(system1));
        var s2 = _knowledgeBase.GetBySlug(Uri.UnescapeDataString(system2)) ?? _knowledgeBase.GetByName(Uri.UnescapeDataString(system2));

        if (s1 == null || s2 == null)
        {
            return View((BeliefSystemComparison?)null);
        }

        var comparison = _knowledgeBase.CompareBeliefSystems(s1.Name, s2.Name);

        return View(comparison);
    }

    // GET: Explore/Timeline
    public IActionResult Timeline()
    {
        var systems = _knowledgeBase.AllSystems
            .Where(s => !string.IsNullOrEmpty(s.Era))
            .OrderBy(s => s.Era)
            .ToList();

        return View(systems);
    }

    // GET: Explore/Map
    public IActionResult Map()
    {
        var systems = _knowledgeBase.AllSystems
            .Where(s => s.Regions.Any())
            .ToList();

        // Group by region
        var systemsByRegion = systems
            .SelectMany(s => s.Regions.Select(r => new { Region = r, System = s }))
            .GroupBy(x => x.Region)
            .ToDictionary(g => g.Key, g => g.Select(x => x.System).ToList());

        ViewBag.SystemsByRegion = systemsByRegion;

        return View(systems);
    }

    // GET: Explore/Categories
    public IActionResult Categories()
    {
        var systemsByCategory = _knowledgeBase.AllSystems
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(systemsByCategory);
    }

    // API endpoint for search autocomplete
    [HttpGet]
    public IActionResult SearchSuggestions(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Json(new List<string>());
        }

        var suggestions = _knowledgeBase.AllSystems
            .Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Name)
            .Take(10)
            .ToList();

        return Json(suggestions);
    }
}
