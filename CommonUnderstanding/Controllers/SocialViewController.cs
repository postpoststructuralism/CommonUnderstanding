using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers;

/// <summary>
/// MVC controller that serves Razor views for Phase 2 social features.
/// API calls from the views go directly to the Social API controllers.
/// </summary>
public class SocialViewController : Controller
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly StakeholderService _stakeholderService;
    private readonly DecisionSupportService _decisionSupportService;
    private readonly SocialArgumentAnalysisService _analysisService;
    private readonly ILogger<SocialViewController> _logger;

    public SocialViewController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        StakeholderService stakeholderService,
        DecisionSupportService decisionSupportService,
        SocialArgumentAnalysisService analysisService,
        ILogger<SocialViewController> logger)
    {
        _dbFactory = dbFactory;
        _stakeholderService = stakeholderService;
        _decisionSupportService = decisionSupportService;
        _analysisService = analysisService;
        _logger = logger;
    }

    // GET /Social/Feed
    public IActionResult Feed()
    {
        ViewData["Title"] = "Social Feed";
        return View("~/Views/Social/Feed.cshtml");
    }

    // GET /Social/ChainBuilder
    public IActionResult ChainBuilder()
    {
        ViewData["Title"] = "Chain Builder";
        return View("~/Views/Social/ChainBuilder.cshtml");
    }

    // GET /Social/WorldviewComposer
    public IActionResult WorldviewComposer()
    {
        ViewData["Title"] = "Worldview Composer";
        return View("~/Views/Social/WorldviewComposer.cshtml");
    }

    // GET /Social/DebateRoom/{id?}
    public async Task<IActionResult> DebateRoom(Guid? id, CancellationToken ct = default)
    {
        ViewData["Title"] = "Debate Room";

        if (id.HasValue)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var room = await db.DebateRooms
                .AsNoTracking()
                .Include(r => r.Contributions)
                    .ThenInclude(c => c.Argument)
                .FirstOrDefaultAsync(r => r.Id == id.Value, ct);

            if (room is not null)
                ViewBag.Room = room;
        }

        return View("~/Views/Social/DebateRoom.cshtml");
    }

    // GET /Social/Detail/{id}
    public async Task<IActionResult> Detail(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var arg = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Include(a => a.Votes)
            .Include(a => a.OutboundLinks)
                .ThenInclude(l => l.TargetArgument)
                    .ThenInclude(a => a!.ClaimProposition)
            .Include(a => a.InboundLinks)
                .ThenInclude(l => l.SourceArgument)
                    .ThenInclude(a => a!.ClaimProposition)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (arg is null || (!arg.IsPublic && arg.UserId != userId))
            return NotFound();

        ViewData["Title"] = arg.Title;
        ViewBag.UserVote = arg.Votes.FirstOrDefault(v => v.UserId == userId);

        // If this social argument was published from a Phase 1 analytical argument,
        // load the full analysis data for the "View Analysis" section.
        if (arg.SourceArgumentId.HasValue)
        {
            var sourceArg = await db.Arguments
                .AsNoTracking()
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Premises)
                        .ThenInclude(p => p.EvidenceItems)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Syllogisms)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Assumptions)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Qualifiers)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Rebuttals)
                .Include(a => a.AdjudicationSummary)
                .FirstOrDefaultAsync(a => a.Id == arg.SourceArgumentId.Value);

            if (sourceArg != null)
            {
                ViewBag.SourceArgument = sourceArg;

                // Load stakeholder data for the analysis section
                try
                {
                    ViewBag.StakeholderPositions = await _stakeholderService.GetPositionsForArgumentAsync(sourceArg.Id);
                    ViewBag.StakeholderConsensus = await _stakeholderService.GetConsensusAsync(sourceArg.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Stakeholder data pre-load skipped for source argument {Id}", sourceArg.Id);
                }

                // Pre-load decision support
                try
                {
                    ViewBag.DecisionSupport = await _decisionSupportService.GenerateAsync(sourceArg.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Decision support pre-load skipped for source argument {Id}", sourceArg.Id);
                }
            }
        }

        // If this is a follow-up (reply) argument, load the parent argument
        // for the "Follow-up Relevance" tab in the analysis section.
        var parentLink = arg.InboundLinks?.FirstOrDefault(l => l.LinkType == Models.Social.LinkType.Reply);
        if (parentLink != null)
        {
            var parentArg = await db.SocialArguments
                .AsNoTracking()
                .Include(a => a.ClaimProposition)
                .FirstOrDefaultAsync(a => a.Id == parentLink.SourceArgumentId);

            if (parentArg != null)
            {
                ViewBag.ParentArgument = parentArg;
            }
        }

        return View("~/Views/Social/Detail.cshtml", arg);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /SocialView/AnalyzeFollowUp
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Manually triggers the full Phase 1 analysis pipeline for a follow-up
    /// (reply) argument that doesn't have analysis yet.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeFollowUp(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var arg = await db.SocialArguments
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (arg == null) return NotFound();
        if (arg.UserId != userId) return Forbid();

        try
        {
            // Use CancellationToken.None so the analysis completes even if the
            // client disconnects — AI decomposition can take 30-60 seconds.
            await _analysisService.AnalyzeSocialArgumentAsync(id, CancellationToken.None);
            TempData["Success"] = "Analysis completed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual analysis failed for follow-up {Id}", id);
            TempData["Error"] = $"Analysis failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /SocialView/ReanalyzeFollowUp
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-runs the full Phase 1 analysis pipeline for a follow-up (reply)
    /// argument that already has analysis, replacing the old analysis data.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReanalyzeFollowUp(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var arg = await db.SocialArguments
            .FirstOrDefaultAsync(a => a.Id == id);

        if (arg == null) return NotFound();
        if (arg.UserId != userId) return Forbid();

        try
        {
            // Use force=true to clear old analysis and re-run the pipeline
            await _analysisService.AnalyzeSocialArgumentAsync(id, CancellationToken.None, force: true);
            TempData["Success"] = "Analysis re-run completed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Re-analysis failed for follow-up {Id}", id);
            TempData["Error"] = $"Re-analysis failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }
}
