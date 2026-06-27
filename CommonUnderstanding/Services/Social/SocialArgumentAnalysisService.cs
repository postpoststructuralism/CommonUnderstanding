using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Runs the full Phase 1 analytical decomposition pipeline for a SocialArgument,
/// creating a linked Argument record with claims, premises, evidence, syllogisms,
/// assumptions, qualifiers, rebuttals, and adjudication.
/// 
/// This enables follow-up (reply) arguments to have the same "View Analysis"
/// functionality as arguments published from the Phase 1 analytical engine.
/// </summary>
public class SocialArgumentAnalysisService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SocialArgumentAnalysisService> _logger;

    public SocialArgumentAnalysisService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<SocialArgumentAnalysisService> logger)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full decomposition pipeline for a SocialArgument and links the
    /// resulting Phase 1 Argument record via SourceArgumentId.
    /// </summary>
    public async Task<Argument?> AnalyzeSocialArgumentAsync(
        Guid socialArgumentId,
        CancellationToken ct = default,
        bool force = false)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var socialArg = await db.SocialArguments
            .Include(a => a.ClaimProposition)
            .Include(a => a.InboundLinks)
            .FirstOrDefaultAsync(a => a.Id == socialArgumentId, ct);

        if (socialArg == null)
        {
            _logger.LogWarning("SocialArgument {Id} not found for analysis", socialArgumentId);
            return null;
        }

        // If already linked to a Phase 1 argument, skip (unless force=true)
        if (socialArg.SourceArgumentId.HasValue)
        {
            if (!force)
            {
                _logger.LogInformation(
                    "SocialArgument {Id} already has SourceArgumentId {SourceId}, skipping analysis",
                    socialArgumentId, socialArg.SourceArgumentId.Value);
                return await db.Arguments.FindAsync(new object[] { socialArg.SourceArgumentId.Value }, ct);
            }

            _logger.LogInformation(
                "Force re-analysis requested for SocialArgument {Id}, clearing old analysis (Argument {OldArgId})",
                socialArgumentId, socialArg.SourceArgumentId.Value);

            // Clear old analysis data from the existing Phase 1 argument so it
            // can be re-populated by the pipeline below.
            var oldArg = await db.Arguments
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
                .FirstOrDefaultAsync(a => a.Id == socialArg.SourceArgumentId.Value, ct);

            if (oldArg != null)
            {
                // Remove all child entities (cascade will handle premises/evidence)
                if (oldArg.AdjudicationSummary != null)
                    db.AdjudicationSummaries.Remove(oldArg.AdjudicationSummary);

                foreach (var claim in oldArg.Claims.ToList())
                    db.Claims.Remove(claim);

                // Reset the argument to draft status so the pipeline re-processes it
                oldArg.Status = ArgumentStatus.Draft;
                oldArg.RawText = ""; // will be set below
                oldArg.Title = socialArg.Title;
                oldArg.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // Old argument was deleted externally — create a new one
                socialArg.SourceArgumentId = null;
                await db.SaveChangesAsync(ct);
            }
        }

        // Build the raw text from the social argument's content
        var rawText = BuildRawText(socialArg);

        // Create or reuse the Phase 1 Argument record
        Argument argument;
        if (socialArg.SourceArgumentId.HasValue)
        {
            // Reusing the existing argument (force re-analysis)
            argument = (await db.Arguments.FindAsync(
                new object[] { socialArg.SourceArgumentId.Value }, ct))!;
            argument.RawText = rawText;
            argument.Title = socialArg.Title;
            argument.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "Reusing Phase 1 Argument {ArgId} for SocialArgument {SocialId} (force re-analysis)",
                argument.Id, socialArgumentId);
        }
        else
        {
            argument = new Argument
            {
                Title = socialArg.Title,
                RawText = rawText,
                Status = ArgumentStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                SubmittedBy = socialArg.UserId
            };
            db.Arguments.Add(argument);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Created Phase 1 Argument {ArgId} for SocialArgument {SocialId}",
                argument.Id, socialArgumentId);
        }

        // Run the decomposition pipeline using scoped services
        using var scope = _scopeFactory.CreateScope();
        var decompositionService = scope.ServiceProvider.GetRequiredService<ArgumentDecompositionService>();
        var validationService = scope.ServiceProvider.GetRequiredService<LogicalValidationService>();
        var adjudicationEngine = scope.ServiceProvider.GetRequiredService<AdjudicationEngine>();

        try
        {
            // Step 1: Decompose
            _logger.LogInformation("Starting decomposition for argument {ArgId}", argument.Id);
            var decomposition = await decompositionService.DecomposeAsync(
                rawText,
                cancellationToken: ct);

            // Update title from extracted claim
            if (!string.IsNullOrWhiteSpace(decomposition.ClaimText))
            {
                var claimTitle = decomposition.ClaimText.Length > 300
                    ? decomposition.ClaimText[..297] + "…"
                    : decomposition.ClaimText;
                argument.Title = claimTitle;
            }

            // Step 2: Validate (fallacy detection)
            _logger.LogInformation("Starting validation for argument {ArgId}", argument.Id);
            var validation = await validationService.ValidateAsync(decomposition, rawText, ct);

            // Step 3: Persist decomposition results
            await PersistDecompositionAsync(db, argument, decomposition, validation, ct);

            argument.Status = ArgumentStatus.Complete;
            argument.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Step 4: Adjudication
            _logger.LogInformation("Starting adjudication for argument {ArgId}", argument.Id);
            await adjudicationEngine.AdjudicateAsync(
                argument.Id,
                cancellationToken: ct);

            // Link the SocialArgument to the new Phase 1 Argument
            socialArg.SourceArgumentId = argument.Id;
            socialArg.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Analysis complete for SocialArgument {SocialId} → Argument {ArgId}",
                socialArgumentId, argument.Id);

            return argument;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Analysis pipeline failed for SocialArgument {SocialId} (Argument {ArgId})",
                socialArgumentId, argument.Id);

            // Mark the argument as failed so it can be retried
            argument.Status = ArgumentStatus.Draft;
            argument.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            throw;
        }
    }

    /// <summary>
    /// Builds a plain-text representation of the social argument suitable for
    /// the Phase 1 decomposition pipeline.
    /// 
    /// For follow-up (reply) arguments, the Title is used as the claim text
    /// since the ClaimProposition is shared with the parent argument.
    /// </summary>
    private static string BuildRawText(SocialArgument socialArg)
    {
        var parts = new List<string>();

        // For follow-up arguments, use the Title as the claim since the
        // ClaimProposition is shared with the parent. For top-level arguments,
        // use the ClaimProposition text if available.
        if (socialArg.InboundLinks?.Any(l => l.LinkType == LinkType.Reply) == true)
        {
            // This is a follow-up — use its own Title as the claim
            if (!string.IsNullOrWhiteSpace(socialArg.Title))
                parts.Add($"Claim: {socialArg.Title}");
        }
        else if (socialArg.ClaimProposition?.Text is { Length: > 0 } claim)
        {
            parts.Add($"Claim: {claim}");
        }

        if (!string.IsNullOrWhiteSpace(socialArg.WarrantText))
            parts.Add($"Warrant: {socialArg.WarrantText}");

        if (!string.IsNullOrWhiteSpace(socialArg.ResolutionText))
            parts.Add($"Resolution: {socialArg.ResolutionText}");

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Persists the decomposition results (claims, premises, syllogisms, etc.)
    /// into the database. Mirrors the logic from ArgumentController.PersistDecompositionAsync.
    /// </summary>
    private static async Task PersistDecompositionAsync(
        ApplicationDbContext db,
        Argument argument,
        DecompositionResult decomposition,
        ValidationReport validation,
        CancellationToken ct)
    {
        var claim = new Claim
        {
            ArgumentId = argument.Id,
            Text = decomposition.ClaimText,
            ClaimType = decomposition.ClaimType
        };
        db.Claims.Add(claim);
        await db.SaveChangesAsync(ct); // Get claim.Id

        // Premises → Propositions
        int sortOrder = 0;
        foreach (var premise in decomposition.Premises)
        {
            var assessment = decomposition.ProvisionalAssessments
                .FirstOrDefault(a => a.PremiseText.Equals(premise, StringComparison.OrdinalIgnoreCase))
                ?? decomposition.ProvisionalAssessments
                    .FirstOrDefault(a => premise.Contains(a.PremiseText, StringComparison.OrdinalIgnoreCase)
                                      || a.PremiseText.Contains(premise, StringComparison.OrdinalIgnoreCase));

            db.Propositions.Add(new Proposition
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

            db.Syllogisms.Add(new Syllogism
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
            db.Assumptions.Add(new Assumption
            {
                ClaimId = claim.Id,
                Text = aDto.Text,
                IsCritical = aDto.IsCritical
            });
        }

        // Qualifiers
        foreach (var q in decomposition.Qualifiers)
        {
            db.Qualifiers.Add(new Qualifier
            {
                ClaimId = claim.Id,
                Text = q
            });
        }

        // Rebuttals
        foreach (var r in decomposition.Rebuttals)
        {
            db.Rebuttals.Add(new Rebuttal
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

            var firstSyllogism = db.Syllogisms
                .Where(s => s.ClaimId == claim.Id)
                .FirstOrDefault();

            if (firstSyllogism != null)
            {
                firstSyllogism.FallaciesDetected = string.IsNullOrWhiteSpace(firstSyllogism.FallaciesDetected)
                    ? fallacyNotes
                    : firstSyllogism.FallaciesDetected + "\n" + fallacyNotes;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}