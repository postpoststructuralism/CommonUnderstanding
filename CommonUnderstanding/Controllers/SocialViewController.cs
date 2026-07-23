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
        ViewData["Title"] = "Latest Contributions";
        return View("~/Views/Social/Feed.cshtml");
    }

    // GET /SocialView/Ranking
    public IActionResult Ranking()
    {
        ViewData["Title"] = "How contributions are ordered";
        return View("~/Views/Social/Ranking.cshtml");
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

        ViewData["Title"] = arg.ClaimProposition?.Text ?? arg.Title;

        var contributionHistory = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.ClaimPropositionId == arg.ClaimPropositionId
                && a.IsPublic
                && !a.IsShadowBanned)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var opposingArgumentIds = arg.InboundLinks
            .Where(l => l.LinkType == LinkType.Contradicts)
            .Select(l => l.SourceArgumentId)
            .Concat(arg.OutboundLinks
                .Where(l => l.LinkType == LinkType.Contradicts)
                .Select(l => l.TargetArgumentId))
            .ToHashSet();

        var supportingArguments = contributionHistory
            .Where(a => !opposingArgumentIds.Contains(a.Id))
            .OrderByDescending(a => a.WilsonScore)
            .ThenByDescending(a => a.UpvoteCount - a.DownvoteCount)
            .Take(3)
            .ToList();

        var opposingArguments = arg.InboundLinks
            .Where(l => l.LinkType == LinkType.Contradicts
                && l.SourceArgument is { IsPublic: true, IsShadowBanned: false })
            .Select(l => l.SourceArgument!)
            .Concat(arg.OutboundLinks
                .Where(l => l.LinkType == LinkType.Contradicts
                    && l.TargetArgument is { IsPublic: true, IsShadowBanned: false })
                .Select(l => l.TargetArgument!))
            .DistinctBy(a => a.Id)
            .OrderByDescending(a => a.WilsonScore)
            .ThenByDescending(a => a.UpvoteCount - a.DownvoteCount)
            .Take(3)
            .ToList();

        Argument? sourceArg = null;

        // If this social argument was published from a Phase 1 analytical argument,
        // load the full analysis data for the "View Analysis" section.
        if (arg.SourceArgumentId.HasValue)
        {
            sourceArg = await db.Arguments
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

        var claims = sourceArg?.Claims ?? Array.Empty<CommonUnderstanding.Models.Claim>();
        var viewModel = new ClaimStateViewModel
        {
            FocusArgument = arg,
            Proposition = arg.ClaimProposition!,
            SupportingArguments = supportingArguments,
            OpposingArguments = opposingArguments,
            ContributionHistory = contributionHistory,
            Evidence = claims
                .SelectMany(c => c.Premises)
                .SelectMany(p => p.EvidenceItems)
                .OrderBy(e => e.Direction)
                .ThenBy(e => e.Tier)
                .ToList(),
            RemainingQuestions = claims
                .SelectMany(c => c.Assumptions)
                .Where(a => !a.IsSupported)
                .OrderByDescending(a => a.IsCritical)
                .ToList(),
            SourceArgument = sourceArg,
            Adjudication = sourceArg?.AdjudicationSummary,
            UserVote = arg.Votes.FirstOrDefault(v => v.UserId == userId)
        };

        return View("~/Views/Social/Detail.cshtml", viewModel);
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
