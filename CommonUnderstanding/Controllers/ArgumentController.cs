using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services;
using System.Security.Claims;
using System.Text.Json;

namespace CommonUnderstanding.Controllers;

[Authorize]
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.Arguments
            .Include(a => a.AdjudicationSummary)
            .Where(a => a.SubmittedBy == userId)
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
            SubmittedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
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

        // If already decomposed and not a forced re-analysis, go to review screen
        if (!force && argument.Status == ArgumentStatus.Complete && argument.Claims.Any())
            return RedirectToAction(nameof(Review), new { id });

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

        Task SendDebugAsync(string message, object? details = null)
            => SendEventAsync("debug", new
            {
                timestamp = DateTime.UtcNow,
                message,
                details
            });

        try
        {
            await SendDebugAsync("AnalyzeStream started", new { argumentId = id, force });

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

            await SendDebugAsync("Argument query completed", new { found = argument is not null });

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
            await SendDebugAsync("Argument marked as decomposing", new { argumentId = argument.Id, status = argument.Status.ToString() });

            // ── Step 1-4: Decomposition with real-time progress ──────────────
            // Phase 1: Narrative-style progress messages (not debug "Step N/5")
            await SendEventAsync("progress", new { step = 0, total = 5, label = "Reading what you wrote…" });
            await SendDebugAsync("Starting decomposition service call", new { rawTextLength = argument.RawText?.Length ?? 0 });

            var decomposition = await _decompositionService.DecomposeAsync(
                argument.RawText,
                onProgress: async (label, step, total) =>
                {
                    // Translate internal labels to user-facing narrative
                    var narrativeLabel = label switch
                    {
                        string s when s.Contains("Extracting central claim", StringComparison.OrdinalIgnoreCase)
                            => "Identifying your central claim…",
                        string s when s.Contains("Decomposing argument structure", StringComparison.OrdinalIgnoreCase)
                            => "Looking at your reasoning — mapping out the supporting points…",
                        _ => label
                    };
                    await SendDebugAsync("Decomposition progress callback", new { step, total, label });
                    await SendEventAsync("progress", new { step, total = 5, label = narrativeLabel });
                },
                onDebug: async (message, details) =>
                {
                    await SendDebugAsync(message, details);
                },
                cancellationToken: ct);

            await SendDebugAsync("Decomposition service call completed", new
            {
                claimLength = decomposition.ClaimText?.Length ?? 0,
                premiseCount = decomposition.Premises.Count,
                syllogismCount = decomposition.Syllogisms.Count,
                assumptionCount = decomposition.Assumptions.Count
            });

            // Update title from the extracted claim now that we have it
            if (!string.IsNullOrWhiteSpace(decomposition.ClaimText))
            {
                var claimTitle = decomposition.ClaimText.Length > 300
                    ? decomposition.ClaimText[..297] + "…"
                    : decomposition.ClaimText;
                argument.Title = claimTitle;
                await _db.SaveChangesAsync(ct);
                await SendDebugAsync("Saved claim title", new { titleLength = claimTitle.Length });
                await SendEventAsync("title", new { title = claimTitle });
            }

            // ── Step 3: Fallacy detection ────────────────────────────────────
            await SendEventAsync("progress", new { step = 3, total = 5, label = "Checking your reasoning for logical gaps…" });
            await SendDebugAsync("Starting validation service call");
            var validation = await _validationService.ValidateAsync(decomposition, argument.RawText, ct);
            await SendDebugAsync("Validation service call completed", new
            {
                fallacyCount = validation.FallaciesDetected.Count,
                syllogismValidationCount = validation.SyllogismValidations.Count,
                overallFormValid = validation.OverallFormValid
            });

            // ── Step 4: Persisting results ───────────────────────────────────
            await SendEventAsync("progress", new { step = 4, total = 5, label = "Saving your argument structure…" });
            await SendDebugAsync("Persisting decomposition results");
            await PersistDecompositionAsync(argument, decomposition, validation);

            argument.Status = ArgumentStatus.Complete;
            argument.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await SendDebugAsync("Argument marked complete", new { argumentId = argument.Id });

            // ── Step 5: Adjudication + detailed narrative ────────────────────
            await SendEventAsync("progress", new { step = 5, total = 5, label = "Checking how your argument connects to the map…" });
            await SendDebugAsync("Starting adjudication service call");
            await _adjudicationEngine.AdjudicateAsync(
                argument.Id,
                onDebug: async (message, details) =>
                {
                    await SendDebugAsync(message, details);
                },
                cancellationToken: ct);
            await SendDebugAsync("Adjudication service call completed", new { argumentId = argument.Id });

            // ── Done ─────────────────────────────────────────────────────────
            // Phase 1: Redirect to review-before-publish, then to "what changed"
            await SendDebugAsync("AnalyzeStream completed successfully", new { redirectId = id });
            await SendEventAsync("complete", new { redirectUrl = Url.Action(nameof(Review), new { id }) });
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
                await SendDebugAsync("AnalyzeStream failed", new { exception = ex.Message, argumentId = id });
                await SendEventAsync("error", new { message = $"AI decomposition failed: {ex.Message}" });
            }
            catch { /* client already gone */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/WhatChanged/{id}  — Phase 1: "what changed" screen
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the map delta caused by the user's contribution — what new
    /// propositions were created, what existing ones were strengthened or
    /// contested, and what the strongest opposing view is.
    /// </summary>
    public async Task<IActionResult> WhatChanged(int id)
    {
        var argument = await _db.Arguments
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
            .Include(a => a.AdjudicationSummary)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (argument == null)
            return NotFound();

        if (argument.Status != ArgumentStatus.Complete)
            return RedirectToAction(nameof(Analyze), new { id });

        var claim = argument.Claims.FirstOrDefault();
        var premises = claim?.Premises.OrderBy(p => p.SortOrder).ToList() ?? new();

        // Build the "what changed" view model
        var model = new WhatChangedViewModel
        {
            ArgumentId = id,
            ArgumentTitle = argument.Title,
            ClaimText = claim?.Text ?? argument.Title,

            // New propositions: all premises from this argument
            NewPropositions = premises.Select(p => new WhatChangedProposition
            {
                Text = p.Text,
                ConfidenceScore = p.ConfidenceScore,
                Status = p.Status.ToString()
            }).ToList(),

            // Existing propositions strengthened/contested: query the graph
            StrengthenedPropositions = new List<WhatChangedProposition>(),
            ContestedPropositions = new List<WhatChangedProposition>(),
            AreasOfAgreement = new List<string>(),
            NewQuestions = new List<string>(),
            StrongestOpposingView = null
        };

        // Query the Common Understanding graph for related propositions
        try
        {
            var graphNodes = await _db.CommonUnderstandingNodes
                .OrderByDescending(n => n.Confidence)
                .ToListAsync();

            foreach (var premise in premises)
            {
                var normalizedKey = NormalizeKeyForComparison(premise.Text);
                var relatedNodes = graphNodes
                    .Where(n => n.NormalizedKey != null &&
                           (n.NormalizedKey.Contains(normalizedKey[..Math.Min(30, normalizedKey.Length)]) ||
                            normalizedKey.Contains(n.NormalizedKey[..Math.Min(30, n.NormalizedKey.Length)])))
                    .Where(n => n.ArgumentIdsJson != null && !n.ArgumentIdsJson.Contains(id.ToString()))
                    .ToList();

                foreach (var node in relatedNodes)
                {
                    if (node.Confidence >= 0.6)
                    {
                        model.StrengthenedPropositions.Add(new WhatChangedProposition
                        {
                            Text = node.Text,
                            ConfidenceScore = node.Confidence,
                            Status = node.Status.ToString(),
                            NodeId = node.Id
                        });
                    }
                    else if (node.Confidence < 0.4)
                    {
                        model.ContestedPropositions.Add(new WhatChangedProposition
                        {
                            Text = node.Text,
                            ConfidenceScore = node.Confidence,
                            Status = node.Status.ToString(),
                            NodeId = node.Id
                        });
                    }
                }
            }

            // Deduplicate
            model.StrengthenedPropositions = model.StrengthenedPropositions
                .GroupBy(p => p.Text)
                .Select(g => g.First())
                .Take(5)
                .ToList();
            model.ContestedPropositions = model.ContestedPropositions
                .GroupBy(p => p.Text)
                .Select(g => g.First())
                .Take(5)
                .ToList();

            // Areas of agreement: high-confidence nodes that align
            var highConfidenceNodes = graphNodes
                .Where(n => n.Confidence >= 0.7 && n.Status == PropositionStatus.Settled)
                .Take(3)
                .ToList();

            foreach (var node in highConfidenceNodes)
            {
                model.AreasOfAgreement.Add(node.Text);
            }

            // New questions: surfaced assumptions from the claim
            if (claim?.Assumptions != null)
            {
                foreach (var assumption in claim.Assumptions)
                {
                    model.NewQuestions.Add(assumption.Text);
                }
            }

            // Strongest opposing view: find a contested node with low confidence
            var opposingNode = graphNodes
                .Where(n => n.Status == PropositionStatus.Contested && n.Confidence < 0.5)
                .OrderBy(n => n.Confidence)
                .FirstOrDefault();

            if (opposingNode != null)
            {
                model.StrongestOpposingView = opposingNode.Text;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fully build WhatChanged view for argument {Id}", id);
        }

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Argument/Review/{id}  — Phase 1: review-before-publish
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the extracted claim and premises for user review before publishing.
    /// The user can confirm or edit the AI's extraction.
    /// </summary>
    public async Task<IActionResult> Review(int id)
    {
        var argument = await _db.Arguments
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Assumptions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (argument == null)
            return NotFound();

        if (argument.Status != ArgumentStatus.Complete)
            return RedirectToAction(nameof(Analyze), new { id });

        var claim = argument.Claims.FirstOrDefault();
        var model = new ReviewViewModel
        {
            ArgumentId = id,
            ClaimText = claim?.Text ?? argument.Title,
            ClaimType = claim?.ClaimType ?? "empirical",
            Premises = claim?.Premises.OrderBy(p => p.SortOrder)
                .Select(p => new ReviewPremise { Id = p.Id, Text = p.Text })
                .ToList() ?? new()
        };

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Argument/Review/{id}  — Phase 1: confirm or edit extraction
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, ReviewViewModel model)
    {
        if (model.Action == "edit")
        {
            // User wants to edit — apply corrections
            var argument = await _db.Arguments
                .Include(a => a.Claims)
                    .ThenInclude(c => c.Premises)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (argument == null) return NotFound();

            var claim = argument.Claims.FirstOrDefault();
            if (claim != null && !string.IsNullOrWhiteSpace(model.ClaimText))
            {
                claim.Text = model.ClaimText.Trim();
                argument.Title = model.ClaimText.Length > 300
                    ? model.ClaimText[..297] + "…"
                    : model.ClaimText;

                // Update premises if provided
                if (model.Premises != null)
                {
                    foreach (var editedPremise in model.Premises)
                    {
                        var existing = claim.Premises.FirstOrDefault(p => p.Id == editedPremise.Id);
                        if (existing != null && !string.IsNullOrWhiteSpace(editedPremise.Text))
                        {
                            existing.Text = editedPremise.Text.Trim();
                        }
                    }
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("User edited AI extraction for argument {Id}", id);
            }

            return RedirectToAction(nameof(WhatChanged), new { id });
        }

        // "publish" action — go straight to what-changed
        return RedirectToAction(nameof(WhatChanged), new { id });
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
    //  POST /Argument/Publish/{id}  — bridge a Phase 1 argument into the social feed
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes an analysed Phase 1 argument to the social feed by creating (or
    /// re-publishing) a linked public SocialArgument. Idempotent: a second publish
    /// of the same argument re-uses the existing social post via SourceArgumentId.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var argument = await _db.Arguments
            .Include(a => a.Claims)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (argument == null)
            return NotFound();

        if (argument.SubmittedBy != userId)
            return Forbid();

        // Idempotent: if already published, just route to the feed.
        var existing = await _db.SocialArguments
            .FirstOrDefaultAsync(a => a.SourceArgumentId == id);
        if (existing != null)
        {
            if (!existing.IsPublic)
            {
                existing.IsPublic = true;
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            TempData["Info"] = "This argument is already on the feed.";
            return RedirectToAction(nameof(View), new { id });
        }

        var claim = argument.Claims.FirstOrDefault();
        var claimText = claim?.Text;
        if (string.IsNullOrWhiteSpace(claimText))
        {
            TempData["Error"] = "Analyse this argument before publishing — it has no claim yet.";
            return RedirectToAction(nameof(View), new { id });
        }

        var claimProp = new SocialProposition
        {
            Text = claimText,
            Type = SocialPropositionType.Claim,
            UserId = userId,
            IsAIGenerated = true,
            IsConfirmed = true
        };
        _db.SocialPropositions.Add(claimProp);
        await _db.SaveChangesAsync();

        var social = new SocialArgument
        {
            Title = argument.Title,
            ClaimPropositionId = claimProp.Id,
            WarrantText = argument.RawText,
            UserId = userId,
            SourceArgumentId = id,
            IsPublic = true,
            Tags = string.IsNullOrWhiteSpace(claim?.ClaimType)
                ? Array.Empty<string>()
                : new[] { claim!.ClaimType! }
        };
        _db.SocialArguments.Add(social);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Published argument {ArgumentId} to social feed as {SocialId} by {UserId}",
            id, social.Id, userId);

        TempData["Info"] = "Published to the social feed.";
        return RedirectToAction(nameof(View), new { id });
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

        var claim = new CommonUnderstanding.Models.Claim
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

    /// <summary>
    /// Normalizes text for fuzzy comparison against graph nodes.
    /// </summary>
    private static string NormalizeKeyForComparison(string text)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            text.ToLowerInvariant().Trim(), @"\s+", " ");
        normalized = normalized.TrimEnd('.', ',', ';', ':', '!', '?');
        return normalized.Length > 500 ? normalized[..500] : normalized;
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

// ─────────────────────────────────────────────
//  Phase 1 ViewModels
// ─────────────────────────────────────────────

/// <summary>
/// ViewModel for the "What Changed" screen shown after analysis completes.
/// </summary>
public class WhatChangedViewModel
{
    public int ArgumentId { get; set; }
    public string ArgumentTitle { get; set; } = string.Empty;
    public string ClaimText { get; set; } = string.Empty;

    public List<WhatChangedProposition> NewPropositions { get; set; } = new();
    public List<WhatChangedProposition> StrengthenedPropositions { get; set; } = new();
    public List<WhatChangedProposition> ContestedPropositions { get; set; } = new();
    public List<string> AreasOfAgreement { get; set; } = new();
    public List<string> NewQuestions { get; set; } = new();
    public string? StrongestOpposingView { get; set; }
}

public class WhatChangedProposition
{
    public string Text { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? NodeId { get; set; }
}

/// <summary>
/// ViewModel for the review-before-publish step.
/// </summary>
public class ReviewViewModel
{
    public int ArgumentId { get; set; }
    public string ClaimText { get; set; } = string.Empty;
    public string ClaimType { get; set; } = "empirical";
    public List<ReviewPremise> Premises { get; set; } = new();
    public string Action { get; set; } = "publish"; // "publish" or "edit"
}

public class ReviewPremise
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}
