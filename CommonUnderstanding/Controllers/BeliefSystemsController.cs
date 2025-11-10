using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class BeliefSystemsController : Controller
{
    private readonly BeliefAnalysisService _analysisService;
    private readonly ILogger<BeliefSystemsController> _logger;
    
    // In-memory storage for demo purposes - replace with database in production
    private static readonly List<BeliefSystem> _beliefSystems = new();
    private static readonly List<BeliefComparison> _comparisons = new();

    public BeliefSystemsController(
        BeliefAnalysisService analysisService,
        ILogger<BeliefSystemsController> logger)
    {
        _analysisService = analysisService;
        _logger = logger;
    }

    // GET: BeliefSystems
    public IActionResult Index()
    {
        return View(_beliefSystems);
    }

    // GET: BeliefSystems/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: BeliefSystems/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BeliefSystem beliefSystem)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Analyze the belief system using AI
                var analyzed = await _analysisService.AnalyzeBeliefSystemAsync(
                    beliefSystem.Name, 
                    beliefSystem.Description);
                
                _beliefSystems.Add(analyzed);
                
                TempData["Success"] = $"Belief system '{beliefSystem.Name}' created and analyzed successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating belief system");
                ModelState.AddModelError("", "Error analyzing belief system. Make sure Ollama is running.");
            }
        }
        return View(beliefSystem);
    }

    // GET: BeliefSystems/Details/5
    public IActionResult Details(string id)
    {
        var beliefSystem = _beliefSystems.FirstOrDefault(bs => bs.Id == id);
        if (beliefSystem == null)
        {
            return NotFound();
        }
        return View(beliefSystem);
    }

    // GET: BeliefSystems/Compare
    public IActionResult Compare()
    {
        ViewBag.BeliefSystems = _beliefSystems;
        return View();
    }

    // POST: BeliefSystems/Compare
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compare(string beliefSystem1Id, string beliefSystem2Id)
    {
        var bs1 = _beliefSystems.FirstOrDefault(bs => bs.Id == beliefSystem1Id);
        var bs2 = _beliefSystems.FirstOrDefault(bs => bs.Id == beliefSystem2Id);

        if (bs1 == null || bs2 == null)
        {
            TempData["Error"] = "Please select two valid belief systems to compare.";
            return RedirectToAction(nameof(Compare));
        }

        try
        {
            var comparison = await _analysisService.CompareBeliefSystemsAsync(bs1, bs2);
            _comparisons.Add(comparison);
            
            return RedirectToAction(nameof(ComparisonResult), new { id = comparison.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing belief systems");
            TempData["Error"] = "Error comparing belief systems. Make sure Ollama is running.";
            return RedirectToAction(nameof(Compare));
        }
    }

    // GET: BeliefSystems/ComparisonResult/5
    public IActionResult ComparisonResult(string id)
    {
        var comparison = _comparisons.FirstOrDefault(c => c.Id == id);
        if (comparison == null)
        {
            return NotFound();
        }
        return View(comparison);
    }

    // GET: BeliefSystems/Comparisons
    public IActionResult Comparisons()
    {
        return View(_comparisons);
    }

    // GET: BeliefSystems/DialogueSuggestions/5
    public async Task<IActionResult> DialogueSuggestions(string id)
    {
        var comparison = _comparisons.FirstOrDefault(c => c.Id == id);
        if (comparison == null)
        {
            return NotFound();
        }

        try
        {
            var suggestions = await _analysisService.GenerateDialogueSuggestionsAsync(comparison);
            ViewBag.Suggestions = suggestions;
            ViewBag.Comparison = comparison;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dialogue suggestions");
            TempData["Error"] = "Error generating suggestions. Make sure Ollama is running.";
            return RedirectToAction(nameof(ComparisonResult), new { id });
        }
    }
}
