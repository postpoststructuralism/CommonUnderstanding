using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social.Plugins;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social.Workers;

/// <summary>
/// Background service that processes newly published SocialArguments through AI validation.
/// - Runs FallacyDetectionPlugin within 60 seconds of publication.
/// - Shadow-bans arguments with ValidityScore &lt; 0.3.
/// - Applies WilsonScore penalty for detected fallacies.
/// - Awards IsAIValidated bonus on high-validity arguments.
/// </summary>
public class AIValidationWorker : BackgroundService
{
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIValidationWorker> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    public AIValidationWorker(
        SingletonDbContextFactory dbFactory,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AIValidationWorker> logger)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AIValidationWorker starting.");

        // Stagger startup
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingArgumentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AIValidationWorker encountered an error.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("AIValidationWorker stopping.");
    }

    private async Task ProcessPendingArgumentsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var fallacyPlugin = scope.ServiceProvider.GetRequiredService<FallacyDetectionPlugin>();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Arguments that are public, not yet AI-validated, and published within the last 60 minutes
        var cutoff = DateTime.UtcNow.AddMinutes(-60);
        var pending = await db.SocialArguments
            .Include(a => a.ClaimProposition)
            .Where(a => a.IsPublic
                     && !a.IsAIValidated
                     && a.AIValidityScore == null
                     && a.CreatedAt >= cutoff)
            .Take(10) // Process in batches to stay within latency budget
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        _logger.LogInformation("AIValidationWorker: validating {Count} arguments.", pending.Count);

        double shadowBanThreshold = _configuration.GetValue("Voting:ShadowBanValidityThreshold", 0.3);

        foreach (var argument in pending)
        {
            await ValidateArgumentAsync(argument, db, fallacyPlugin, shadowBanThreshold, ct);
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("AIValidationWorker: completed validation for {Count} arguments.", pending.Count);
    }

    private async Task ValidateArgumentAsync(
        SocialArgument argument,
        ApplicationDbContext db,
        FallacyDetectionPlugin fallacyPlugin,
        double shadowBanThreshold,
        CancellationToken ct)
    {
        try
        {
            string text = BuildArgumentText(argument);
            var result = await fallacyPlugin.DetectFallaciesAsync(
                text, string.Empty, string.Empty, ct);

            argument.AIValidityScore = result.ValidityScore;
            argument.IsAIValidated = true;

            if (result.Fallacies.Count > 0)
            {
                argument.AIFallacyFlags = System.Text.Json.JsonSerializer.Serialize(
                    result.Fallacies.Select(f => f.Name));
            }

            // Shadow ban if below threshold
            if (result.ValidityScore < shadowBanThreshold)
            {
                argument.IsShadowBanned = true;
                _logger.LogInformation(
                    "Shadow-banned argument {Id} (ValidityScore: {Score:F2})",
                    argument.Id, result.ValidityScore);
            }

            // Apply fallacy Wilson score penalty
            if (result.Fallacies.Count > 0 && !argument.IsShadowBanned)
            {
                argument.WilsonScore = Math.Max(0, argument.WilsonScore - 0.1 * result.Fallacies.Count);
            }

            // ── Follow-up relevance assessment ──────────────────────────────
            // If this argument is a reply to another argument, assess how relevant
            // and effective it is at addressing the parent.
            var inboundLink = await db.ArgumentLinks
                .Include(l => l.SourceArgument)
                    .ThenInclude(a => a.ClaimProposition)
                .FirstOrDefaultAsync(l =>
                    l.TargetArgumentId == argument.Id &&
                    l.LinkType == LinkType.Reply, ct);

            if (inboundLink?.SourceArgument != null)
            {
                var parent = inboundLink.SourceArgument;
                string parentText = BuildArgumentText(parent);
                string replyText = BuildArgumentText(argument);

                var relevanceResult = await fallacyPlugin.AssessFollowUpRelevanceAsync(
                    replyText, parentText, ct);

                argument.FollowUpRelevanceScore = relevanceResult.RelevanceScore;
                argument.FollowUpEffectivenessNotes = relevanceResult.EffectivenessNotes;

                _logger.LogInformation(
                    "Follow-up relevance for {ReplyId} → {ParentId}: {Score:F2} — {Notes}",
                    argument.Id, parent.Id, relevanceResult.RelevanceScore, relevanceResult.EffectivenessNotes);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the whole batch on a single failure; mark as validated to prevent retry
            argument.IsAIValidated = true;
            argument.AIValidityScore = 0.5; // Neutral fallback
            _logger.LogWarning(ex, "AI validation failed for argument {Id}.", argument.Id);
        }
    }

    private static string BuildArgumentText(SocialArgument arg) =>
        $"Claim: {arg.ClaimProposition?.Text ?? "(no claim)"}\nWarrant: {arg.WarrantText}";
}
