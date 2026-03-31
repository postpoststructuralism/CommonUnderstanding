using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using System.Text.Json;

namespace CommonUnderstanding.Controllers;

public class ArgumentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ArgumentDecompositionService _decompositionService;
    private readonly LogicalValidationService _validationService;
    private readonly AdjudicationEngine _adjudicationEngine;
    private readonly EvidenceClassificationService _evidenceClassifier;
    private readonly StakeholderService _stakeholderService;
    private readonly DecisionSupportService _decisionSupportService;
    private readonly ComparativeAnalysisService _comparativeAnalysisService;
    private readonly ILogger<ArgumentController> _logger;

    public ArgumentController(
        ApplicationDbContext db,
        ArgumentDecompositionService decompositionService,
        LogicalValidationService validationService,
        AdjudicationEngine adjudicationEngine,
        EvidenceClassificationService evidenceClassifier,
        StakeholderService stakeholderService,
        DecisionSupportService decisionSupportService,
        ComparativeAnalysisService comparativeAnalysisService,
        ILogger<ArgumentController> logger)
    {
        _db = db;
        _decompositionService = decompositionService;
        _validationService = validationService;
        _adjudicationEngine = adjudicationEngine;
        _evidenceClassifier = evidenceClassifier;
        _stakeholderService = stakeholderService;
        _decisionSupportService = decisionSupportService;
        _comparativeAnalysisService = comparativeAnalysisService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all submitted arguments with optional filtering.
    /// </summary>
    public async Task<IActionResult> Index(string? status, string? recommendation, string? sort)
    {
        var query = _db.Arguments
            .Include(a => a.AdjudicationSummary)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ArgumentStatus>(status, out var statusEnum))
            query = query.Where(a => a.Status == statusEnum);

        if (!string.IsNullOrEmpty(recommendation) && Enum.TryParse<DecisionRecommendation>(recommendation, out var recEnum))
            query = query.Where(a => a.AdjudicationSummary != null && a.AdjudicationSummary.Recommendation == recEnum);

        query = sort == "confidence"
            ? query.OrderByDescending(a => a.AdjudicationSummary != null ? (double?)a.AdjudicationSummary.OverallConfidence : null)
            : query.OrderByDescending(a => a.CreatedAt);

        ViewBag.Status = status;
        ViewBag.Recommendation = recommendation;
        ViewBag.Sort = sort;

        return View(await query.ToListAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/Submit
    // ─────────────────────────────────────────────────────────────────────────

    public IActionResult Submit()
    {
        return View(new ArgumentSubmitModel());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Argument/Submit
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ArgumentSubmitModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Generate a working title from the first sentence / 80 chars until AI replaces it
        var rawText = model.ArgumentText.Trim();
        var firstSentenceEnd = rawText.IndexOfAny(new[] { '.', '!', '?' });
        var provisionalTitle = firstSentenceEnd > 0 && firstSentenceEnd <= 120
            ? rawText[..firstSentenceEnd].Trim()
            : rawText[..Math.Min(80, rawText.Length)].Trim();
        if (provisionalTitle.Length > 100)
            provisionalTitle = provisionalTitle[..97] + "…";

        var argument = new Argument
        {
            Title = provisionalTitle,
            RawText = rawText,
            SubmittedBy = model.SubmittedBy?.Trim(),
            Status = ArgumentStatus.Draft
        };

        _db.Arguments.Add(argument);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Analyze), new { id = argument.Id });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/Analyze/{id}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the streaming analysis page. The actual work happens via the SSE endpoint.
    /// </summary>
    public async Task<IActionResult> Analyze(int id, bool force = false)
    {
        var argument = await _db.Arguments
            .Include(a => a.Claims)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (argument == null)
            return NotFound();

        // If already decomposed and not a forced re-analysis, go straight to view
        if (!force && argument.Status == ArgumentStatus.Complete && argument.Claims.Any())
            return RedirectToAction(nameof(View), new { id });

        ViewBag.ArgumentId = id;
        ViewBag.ArgumentTitle = argument.Title;
        ViewBag.ForceReanalyze = force;
        return View();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/AnalyzeStream/{id} — SSE real-time progress
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Server-Sent Events endpoint that performs decomposition and streams
    /// step-by-step progress to the browser in real time.
    /// </summary>
    [HttpGet]
    public async Task AnalyzeStream(int id, bool force = false)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var ct = HttpContext.RequestAborted;

        async Task SendEventAsync(string eventType, object data)
        {
            var json = JsonSerializer.Serialize(data);
            await Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        try
        {
            var argument = await _db.Arguments
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Premises)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Syllogisms)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Assumptions)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Qualifiers)
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Rebuttals)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (argument == null)
            {
                await SendEventAsync("error", new { message = "Argument not found." });
                return;
            }

            // If already complete and not a forced re-analysis, just signal done
            if (!force && argument.Status == ArgumentStatus.Complete && argument.Claims.Any())
            {
                await SendEventAsync("complete", new { redirectUrl = Url.Action(nameof(View), new { id }) });
                return;
            }

            argument.Status = ArgumentStatus.Decomposing;
            argument.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // ── Step 1-4: Decomposition with real-time progress ──────────────
            await SendEventAsync("progress", new { step = 0, total = 5, label = "Starting AI decomposition…" });

            var decomposition = await _decompositionService.DecomposeAsync(
                argument.RawText,
                onProgress: async (label, step, total) =>
                {
                    await SendEventAsync("progress", new { step, total = 5, label });
                },
                cancellationToken: ct);

            // Update title from the extracted claim now that we have it
            if (!string.IsNullOrWhiteSpace(decomposition.ClaimText))
            {
                var claimTitle = decomposition.ClaimText.Length > 300
                    ? decomposition.ClaimText[..297] + "…"
                    : decomposition.ClaimText;
                argument.Title = claimTitle;
                await _db.SaveChangesAsync(ct);
                await SendEventAsync("title", new { title = claimTitle });
            }

            // ── Step 3: Fallacy detection ────────────────────────────────────
            await SendEventAsync("progress", new { step = 3, total = 5, label = "Detecting logical fallacies…" });
            var validation = await _validationService.ValidateAsync(decomposition, argument.RawText, ct);

            // ── Step 4: Persisting results ───────────────────────────────────
            await SendEventAsync("progress", new { step = 4, total = 5, label = "Persisting results…" });
            await PersistDecompositionAsync(argument, decomposition, validation);

            argument.Status = ArgumentStatus.Complete;
            argument.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // ── Step 5: Adjudication + detailed narrative ────────────────────
            await SendEventAsync("progress", new { step = 5, total = 5, label = "Running adjudication & generating analysis…" });
            await _adjudicationEngine.AdjudicateAsync(argument.Id, ct);

            // ── Done ─────────────────────────────────────────────────────────
            await SendEventAsync("complete", new { redirectUrl = Url.Action(nameof(View), new { id }) });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Analysis stream canceled for argument {Id} (client disconnected)", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming decomposition failed for argument {Id}", id);

            // Reset status so user can retry
            var arg = await _db.Arguments.FindAsync(id);
            if (arg != null)
            {
                arg.Status = ArgumentStatus.Draft;
                await _db.SaveChangesAsync();
            }

            try
            {
                await SendEventAsync("error", new { message = $"AI decomposition failed: {ex.Message}" });
            }
            catch { /* client already gone */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/View/{id}
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> View(int id)
    {
        var argument = await _db.Arguments
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
            .FirstOrDefaultAsync(a => a.Id == id);

        if (argument == null)
            return NotFound();

        // Load stakeholder data for the positions panel
        ViewBag.StakeholderPositions = await _stakeholderService.GetPositionsForArgumentAsync(id);
        ViewBag.StakeholderConsensus = await _stakeholderService.GetConsensusAsync(id);

        // Pre-load decision support for the Decision tab (no LLM — pure calculation)
        try
        {
            ViewBag.DecisionSupport = await _decisionSupportService.GenerateAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision support pre-load skipped for argument {Id}", id);
        }

        return View(argument);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Argument/AddEvidence
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvidence(AddEvidenceModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(View), new { id = model.ArgumentId });

        var proposition = await _db.Propositions
            .Include(p => p.Claim)
            .FirstOrDefaultAsync(p => p.Id == model.PropositionId);

        if (proposition == null)
            return NotFound();

        // Auto-classify if the user left the tier at default (T5) and direction neutral
        var tier = model.Tier;
        var direction = model.Direction;
        if (model.AutoClassify)
        {
            try
            {
                var classification = await _evidenceClassifier.ClassifyAsync(
                    model.Citation, proposition.Text);
                tier = classification.Tier;
                direction = classification.Direction;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-classification failed; using user-supplied values");
            }
        }

        var item = new EvidenceItem
        {
            PropositionId = model.PropositionId,
            Citation = model.Citation.Trim(),
            SourceUri = string.IsNullOrWhiteSpace(model.SourceUri) ? null : model.SourceUri.Trim(),
            DOI = string.IsNullOrWhiteSpace(model.DOI) ? null : model.DOI.Trim(),
            Tier = tier,
            Direction = direction,
            EffectSize = model.EffectSize,
            SampleSize = model.SampleSize,
            ReplicationStatus = model.ReplicationStatus,
            PublicationYear = model.PublicationYear,
            AddedBy = model.AddedBy?.Trim()
        };

        _db.EvidenceItems.Add(item);
        proposition.EvidenceCount += 1;
        await _db.SaveChangesAsync();

        // Full adjudication pass — updates all proposition confidences + AdjudicationSummary
        await _adjudicationEngine.AdjudicateAsync(model.ArgumentId);

        return RedirectToAction(nameof(View), new { id = model.ArgumentId });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Argument/RegisterPosition
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPosition(RegisterPositionModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(View), new { id = model.ArgumentId });

        try
        {
            var stakeholder = await _stakeholderService.RegisterOrGetAsync(
                model.StakeholderName, model.Role, model.Organization);

            await _stakeholderService.RecordPositionAsync(
                stakeholder.Id,
                model.ArgumentId,
                model.Position,
                model.Reasoning,
                isAnonymous: model.IsAnonymous);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record stakeholder position for argument {Id}", model.ArgumentId);
            TempData["Error"] = "Failed to record your position. Please try again.";
        }

        return RedirectToAction(nameof(View), new { id = model.ArgumentId });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/DecisionSupport/{id}
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> DecisionSupport(int id)
    {
        var argument = await _db.Arguments
            .Include(a => a.AdjudicationSummary)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (argument == null)
            return NotFound();

        try
        {
            var result = await _decisionSupportService.GenerateAsync(id);
            ViewBag.Argument = argument;
            return View(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decision support generation failed for argument {Id}", id);
            TempData["Error"] = "Failed to generate decision support report.";
            return RedirectToAction(nameof(View), new { id });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Argument/Delete/{id}
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var argument = await _db.Arguments.FindAsync(id);
        if (argument != null)
        {
            _db.Arguments.Remove(argument);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/Compare
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Compare()
    {
        var arguments = await _db.Arguments
            .Include(a => a.AdjudicationSummary)
            .Where(a => a.Status == ArgumentStatus.Complete)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        ViewBag.RecentComparisons = await _db.ArgumentComparisons
            .Include(c => c.ArgumentA)
            .Include(c => c.ArgumentB)
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View(arguments);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Argument/Compare
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Compare(int argumentAId, int argumentBId)
    {
        if (argumentAId == argumentBId)
        {
            TempData["Error"] = "Please select two different arguments to compare.";
            return RedirectToAction(nameof(Compare));
        }

        try
        {
            var comparison = await _comparativeAnalysisService.CompareAsync(
                argumentAId, argumentBId, HttpContext.RequestAborted);
            return RedirectToAction(nameof(CompareView), new { id = comparison.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Comparative analysis failed for arguments {A} and {B}", argumentAId, argumentBId);
            TempData["Error"] = "Comparison analysis failed. Please ensure both arguments have been fully analysed.";
            return RedirectToAction(nameof(Compare));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/CompareView/{id}
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> CompareView(int id)
    {
        var comparison = await _db.ArgumentComparisons
            .Include(c => c.ArgumentA)
                .ThenInclude(a => a.AdjudicationSummary)
            .Include(c => c.ArgumentB)
                .ThenInclude(a => a.AdjudicationSummary)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comparison == null)
            return NotFound();

        return View(comparison);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task PersistDecompositionAsync(
        Argument argument,
        DecompositionResult decomposition,
        ValidationReport validation)
    {
        // Remove any existing claims for this argument (idempotent re-run)
        var existingClaims = await _db.Claims.Where(c => c.ArgumentId == argument.Id).ToListAsync();
        _db.Claims.RemoveRange(existingClaims);

        var claim = new Claim
        {
            ArgumentId = argument.Id,
            Text = decomposition.ClaimText,
            ClaimType = decomposition.ClaimType
        };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(); // Get claim.Id

        // Premises → Propositions
        int sortOrder = 0;
        foreach (var premise in decomposition.Premises)
        {
            // Match a provisional assessment for this premise (if available)
            var assessment = decomposition.ProvisionalAssessments
                .FirstOrDefault(a => a.PremiseText.Equals(premise, StringComparison.OrdinalIgnoreCase))
                ?? decomposition.ProvisionalAssessments
                    .FirstOrDefault(a => premise.Contains(a.PremiseText, StringComparison.OrdinalIgnoreCase)
                                      || a.PremiseText.Contains(premise, StringComparison.OrdinalIgnoreCase));

            _db.Propositions.Add(new Proposition
            {
                ClaimId = claim.Id,
                Text = premise,
                SortOrder = sortOrder++,
                Status = PropositionStatus.Unevaluated,
                ProvisionalAssessment = assessment?.Assessment,
                ProvisionalConfidence = assessment?.Confidence
            });
        }

        // Syllogisms
        sortOrder = 0;
        foreach (var sDto in decomposition.Syllogisms)
        {
            var validationResult = validation.SyllogismValidations
                .FirstOrDefault(v => v.Syllogism == sDto);

            _db.Syllogisms.Add(new Syllogism
            {
                ClaimId = claim.Id,
                MajorPremise = sDto.MajorPremise,
                MinorPremise = sDto.MinorPremise,
                Conclusion = sDto.Conclusion,
                InferenceType = sDto.InferenceType,
                IsValidForm = validationResult?.IsValid ?? true,
                FallaciesDetected = validationResult?.Issues.Any() == true
                    ? string.Join("\n", validationResult.Issues)
                    : null,
                SortOrder = sortOrder++
            });
        }

        // Assumptions
        foreach (var aDto in decomposition.CriticalAssumptions)
        {
            _db.Assumptions.Add(new Assumption
            {
                ClaimId = claim.Id,
                Text = aDto.Text,
                IsCritical = aDto.IsCritical
            });
        }

        // Qualifiers
        foreach (var q in decomposition.Qualifiers)
        {
            _db.Qualifiers.Add(new Qualifier
            {
                ClaimId = claim.Id,
                Text = q
            });
        }

        // Rebuttals
        foreach (var r in decomposition.Rebuttals)
        {
            _db.Rebuttals.Add(new Rebuttal
            {
                ClaimId = claim.Id,
                Text = r.Text,
                Strength = r.Strength
            });
        }

        // Persist fallacy findings into the first affected syllogism's notes
        if (validation.FallaciesDetected.Any())
        {
            var fallacyNotes = string.Join("\n", validation.FallaciesDetected
                .Select(f => $"{f.FallacyName}: {f.Explanation}"));

            var firstSyllogism = await _db.Syllogisms
                .Where(s => s.ClaimId == claim.Id)
                .FirstOrDefaultAsync();

            if (firstSyllogism != null)
                firstSyllogism.FallaciesDetected = (firstSyllogism.FallaciesDetected ?? "") + "\n" + fallacyNotes;
        }

        await _db.SaveChangesAsync();
    }
}

// ─────────────────────────────────────────────
//  View models
// ─────────────────────────────────────────────

public class ArgumentSubmitModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(30, ErrorMessage = "Please provide at least 30 characters of argument text.")]
    public string ArgumentText { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? SubmittedBy { get; set; }
}

public class RegisterPositionModel
{
    public int ArgumentId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(150)]
    public string StakeholderName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Role { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(150)]
    public string? Organization { get; set; }

    public StakeholderPositionType Position { get; set; } = StakeholderPositionType.Undecided;

    [System.ComponentModel.DataAnnotations.MaxLength(2000)]
    public string? Reasoning { get; set; }

    public bool IsAnonymous { get; set; }
}

public class AddEvidenceModel
{
    public int ArgumentId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public int PropositionId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string Citation { get; set; } = string.Empty;

    public string? SourceUri { get; set; }
    public string? DOI { get; set; }
    public EvidenceTier Tier { get; set; } = EvidenceTier.T5_CaseStudy;
    public EvidenceDirection Direction { get; set; } = EvidenceDirection.Supports;
    public double? EffectSize { get; set; }
    public int? SampleSize { get; set; }
    public string? ReplicationStatus { get; set; }
    public int? PublicationYear { get; set; }
    public string? AddedBy { get; set; }
    public bool AutoClassify { get; set; }
}
