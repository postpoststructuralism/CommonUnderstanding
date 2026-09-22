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
    private readonly WorldviewInsightService _worldviewInsightService;
    private readonly ILogger<SocialViewController> _logger;

    public SocialViewController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        StakeholderService stakeholderService,
        DecisionSupportService decisionSupportService,
        SocialArgumentAnalysisService analysisService,
        WorldviewInsightService worldviewInsightService,
        ILogger<SocialViewController> logger)
    {
        _dbFactory = dbFactory;
        _stakeholderService = stakeholderService;
        _decisionSupportService = decisionSupportService;
        _analysisService = analysisService;
        _worldviewInsightService = worldviewInsightService;
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

    public IActionResult ChainBuilder()
    {
        return RedirectToAction(nameof(Feed));
    }

    // GET /SocialView/WorldviewComposer
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> WorldviewComposer(CancellationToken ct = default)
    {
        ViewData["Title"] = "Worldview Composer";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var insight = await _worldviewInsightService.BuildAsync(userId, ct);
        return View("~/Views/Social/WorldviewComposer.cshtml", insight);
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
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var shell = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.Id == id && (a.IsPublic || a.UserId == userId))
            .Select(a => new ClaimDetailShellViewModel
            {
                ArgumentId = a.Id,
                Title = a.Title,
                PropositionText = a.ClaimProposition != null ? a.ClaimProposition.Text : a.Title,
                Summary = a.ResolutionText ?? a.WarrantText,
                UpdatedAt = a.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (shell is null)
            return NotFound();

        ViewData["Title"] = shell.PropositionText;
        return View("~/Views/Social/DetailShell.cshtml", shell);
    }

    // GET /SocialView/AnalysisSummary/{id}
    [HttpGet]
    public async Task<IActionResult> AnalysisSummary(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var argument = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.Id == id && (a.IsPublic || a.UserId == userId))
            .Select(a => new
            {
                a.SourceArgumentId,
                PropositionText = a.ClaimProposition != null ? a.ClaimProposition.Text : a.Title,
                CurrentUnderstanding = a.ResolutionText ?? a.WarrantText
            })
            .FirstOrDefaultAsync(ct);

        if (argument is null)
            return NotFound();

        if (!argument.SourceArgumentId.HasValue)
        {
            return PartialView("~/Views/Social/_AnalysisSummary.cshtml", new ClaimAnalysisSummaryViewModel
            {
                PropositionText = argument.PropositionText,
                StateLabel = "Unresolved",
                CurrentUnderstanding = argument.CurrentUnderstanding
            });
        }

        var sourceId = argument.SourceArgumentId.Value;
        var adjudication = await db.AdjudicationSummaries
            .AsNoTracking()
            .Where(a => a.ArgumentId == sourceId)
            .Select(a => new { a.OverallConfidence, a.Recommendation, a.ReasoningTrace })
            .FirstOrDefaultAsync(ct);
        var premiseCount = await db.Propositions
            .AsNoTracking()
            .CountAsync(p => p.Claim!.ArgumentId == sourceId, ct);
        var evidenceCount = await db.EvidenceItems
            .AsNoTracking()
            .CountAsync(e => e.Proposition!.Claim!.ArgumentId == sourceId, ct);
        var criticalGapCount = await db.Assumptions
            .AsNoTracking()
            .CountAsync(a => a.Claim!.ArgumentId == sourceId && a.IsCritical && !a.IsSupported, ct);
        var contested = adjudication?.Recommendation is DecisionRecommendation.Investigate or DecisionRecommendation.Defer;

        return PartialView("~/Views/Social/_AnalysisSummary.cshtml", new ClaimAnalysisSummaryViewModel
        {
            PropositionText = argument.PropositionText,
            StateLabel = adjudication is null ? "Unresolved" : contested ? "Contested" : "Provisionally settled",
            CurrentUnderstanding = adjudication?.ReasoningTrace ?? argument.CurrentUnderstanding,
            PremiseCount = premiseCount,
            EvidenceCount = evidenceCount,
            CriticalGapCount = criticalGapCount,
            OverallConfidence = adjudication?.OverallConfidence,
            Recommendation = adjudication?.Recommendation
        });
    }

    // GET /SocialView/AnalysisDetail/{id}
    [HttpGet]
    public async Task<IActionResult> AnalysisDetail(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var argument = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.Id == id && (a.IsPublic || a.UserId == userId))
            .Select(a => new
            {
                a.SourceArgumentId,
                PropositionText = a.ClaimProposition != null ? a.ClaimProposition.Text : a.Title
            })
            .FirstOrDefaultAsync(ct);

        if (argument is null)
            return NotFound();

        if (!argument.SourceArgumentId.HasValue)
        {
            return PartialView("~/Views/Social/_AnalysisDetail.cshtml", new ClaimAnalysisDetailViewModel
            {
                PropositionText = argument.PropositionText,
                Claims = Array.Empty<CommonUnderstanding.Models.Claim>(),
                Premises = Array.Empty<Proposition>(),
                Evidence = Array.Empty<EvidenceItem>(),
                Syllogisms = Array.Empty<Syllogism>(),
                Assumptions = Array.Empty<Assumption>(),
                Qualifiers = Array.Empty<Qualifier>(),
                Rebuttals = Array.Empty<Rebuttal>()
            });
        }

        var sourceId = argument.SourceArgumentId.Value;
        var sourceArgument = await db.Arguments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == sourceId, ct);
        var claims = await db.Claims.AsNoTracking().Where(c => c.ArgumentId == sourceId).OrderBy(c => c.Id).ToListAsync(ct);
        var premises = new List<Proposition>();
        var syllogisms = new List<Syllogism>();
        var assumptions = new List<Assumption>();
        var qualifiers = new List<Qualifier>();
        var rebuttals = new List<Rebuttal>();
        foreach (var claim in claims)
        {
            premises.AddRange(await db.Propositions.AsNoTracking().Where(p => p.ClaimId == claim.Id).OrderBy(p => p.SortOrder).ToListAsync(ct));
            syllogisms.AddRange(await db.Syllogisms.AsNoTracking().Where(s => s.ClaimId == claim.Id).OrderBy(s => s.SortOrder).ToListAsync(ct));
            assumptions.AddRange(await db.Assumptions.AsNoTracking().Where(a => a.ClaimId == claim.Id).ToListAsync(ct));
            qualifiers.AddRange(await db.Qualifiers.AsNoTracking().Where(q => q.ClaimId == claim.Id).ToListAsync(ct));
            rebuttals.AddRange(await db.Rebuttals.AsNoTracking().Where(r => r.ClaimId == claim.Id).ToListAsync(ct));
        }
        var evidence = await db.EvidenceItems.AsNoTracking().Where(e => e.Proposition!.Claim!.ArgumentId == sourceId).OrderBy(e => e.Direction).ThenBy(e => e.Tier).ToListAsync(ct);
        var adjudication = await db.AdjudicationSummaries.AsNoTracking().FirstOrDefaultAsync(a => a.ArgumentId == sourceId, ct);

        return PartialView("~/Views/Social/_AnalysisDetail.cshtml", new ClaimAnalysisDetailViewModel
        {
            SourceArgument = sourceArgument,
            PropositionText = argument.PropositionText,
            Claims = claims,
            Premises = premises,
            Evidence = evidence,
            Syllogisms = syllogisms,
            Assumptions = assumptions,
            Qualifiers = qualifiers,
            Rebuttals = rebuttals,
            Adjudication = adjudication
        });
    }

    // GET /SocialView/SocialContext/{id}
    [HttpGet]
    public async Task<IActionResult> SocialContext(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var focus = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.Id == id && (a.IsPublic || a.UserId == userId))
            .Select(a => new
            {
                a.Id,
                a.ClaimPropositionId,
                PropositionText = a.ClaimProposition != null ? a.ClaimProposition.Text : a.Title
            })
            .FirstOrDefaultAsync(ct);
        if (focus is null)
            return NotFound();

        var history = await db.SocialArguments.AsNoTracking()
            .Where(a => a.ClaimPropositionId == focus.ClaimPropositionId && a.IsPublic && !a.IsShadowBanned)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new SocialContextHistoryViewModel
            {
                Id = a.Id,
                Title = a.Title,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);
        var outboundOpposingArguments = db.ArgumentLinks
            .AsNoTracking()
            .Where(l => l.LinkType == LinkType.Contradicts && l.SourceArgumentId == id)
            .Select(l => l.TargetArgument!);
        var inboundOpposingArguments = db.ArgumentLinks
            .AsNoTracking()
            .Where(l => l.LinkType == LinkType.Contradicts && l.TargetArgumentId == id)
            .Select(l => l.SourceArgument!);
        var opposingArguments = await outboundOpposingArguments
            .Concat(inboundOpposingArguments)
            .Where(a => a.IsPublic && !a.IsShadowBanned)
            .OrderByDescending(a => a.WilsonScore)
            .ThenByDescending(a => a.UpvoteCount - a.DownvoteCount)
            .Select(a => new SocialContextArgumentViewModel
            {
                Id = a.Id,
                Title = a.Title,
                WarrantText = a.WarrantText
            })
            .Take(3)
            .ToListAsync(ct);
        var supportingArguments = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.ClaimPropositionId == focus.ClaimPropositionId
                && a.IsPublic
                && !a.IsShadowBanned
                && !db.ArgumentLinks.Any(l => l.LinkType == LinkType.Contradicts
                    && ((l.SourceArgumentId == id && l.TargetArgumentId == a.Id)
                        || (l.TargetArgumentId == id && l.SourceArgumentId == a.Id))))
            .OrderByDescending(a => a.WilsonScore)
            .ThenByDescending(a => a.UpvoteCount - a.DownvoteCount)
            .Select(a => new SocialContextArgumentViewModel
            {
                Id = a.Id,
                Title = a.Title,
                WarrantText = a.WarrantText
            })
            .Take(3)
            .ToListAsync(ct);

        return PartialView("~/Views/Social/_SocialContext.cshtml", new ClaimSocialContextViewModel
        {
            FocusArgumentId = focus.Id,
            PropositionId = focus.ClaimPropositionId,
            PropositionText = focus.PropositionText,
            SupportingArguments = supportingArguments,
            OpposingArguments = opposingArguments,
            ContributionHistory = history
        });
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
