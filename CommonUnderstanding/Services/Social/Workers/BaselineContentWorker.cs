using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social.Workers;

public sealed class BaselineContentWorker : BackgroundService
{
    private const string ServiceAccountUsername = "common-understanding-ai";
    private const string ServiceAccountDisplayName = "Common Understanding AI";

    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BeliefSystemKnowledgeBase _knowledgeBase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BaselineContentWorker> _logger;

    public BaselineContentWorker(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IServiceScopeFactory scopeFactory,
        BeliefSystemKnowledgeBase knowledgeBase,
        IConfiguration configuration,
        ILogger<BaselineContentWorker> logger)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _knowledgeBase = knowledgeBase;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("BaselineContent:Enabled", false))
        {
            _logger.LogInformation("Baseline content generation is disabled.");
            return;
        }

        var startupDelay = TimeSpan.FromSeconds(
            Math.Max(0, _configuration.GetValue("BaselineContent:StartupDelaySeconds", 90)));
        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _configuration.GetValue("BaselineContent:PollingIntervalMinutes", 30)));

        await Task.Delay(startupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Baseline content batch failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var argumentsPerSystem = Math.Clamp(
            _configuration.GetValue("BaselineContent:ArgumentsPerBeliefSystem", 2), 1, 5);
        var maxSystemsPerBatch = Math.Clamp(
            _configuration.GetValue("BaselineContent:MaxBeliefSystemsPerBatch", 1), 1, 10);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var account = await EnsureServiceAccountAsync(db, cancellationToken);

        var systemsToProcess = new List<CanonicalBeliefSystem>();
        foreach (var system in _knowledgeBase.AllSystems.OrderBy(system => system.Name))
        {
            var prefix = BuildSourcePrefix(system);
            var existingCount = await db.SocialArguments
                .CountAsync(argument =>
                    argument.GenerationSourceKey != null &&
                    argument.GenerationSourceKey.StartsWith(prefix), cancellationToken);

            if (existingCount < argumentsPerSystem)
                systemsToProcess.Add(system);

            if (systemsToProcess.Count >= maxSystemsPerBatch)
                break;
        }

        foreach (var system in systemsToProcess)
        {
            await ProcessBeliefSystemAsync(
                system,
                account.Id,
                argumentsPerSystem,
                cancellationToken);
        }
    }

    private async Task ProcessBeliefSystemAsync(
        CanonicalBeliefSystem system,
        string serviceAccountId,
        int argumentsPerSystem,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<BaselineContentGenerationService>();
        var analysis = scope.ServiceProvider.GetRequiredService<SocialArgumentAnalysisService>();

        var existingBySlot = await LoadExistingArgumentsAsync(system, cancellationToken);
        foreach (var existing in existingBySlot.Values.Where(argument => !argument.SourceArgumentId.HasValue))
            await analysis.AnalyzeSocialArgumentAsync(existing.Id, cancellationToken);

        var missingSlots = Enumerable.Range(1, argumentsPerSystem)
            .Where(slot => !existingBySlot.ContainsKey(slot))
            .ToList();
        if (missingSlots.Count == 0)
            return;

        var generated = await generator.GenerateAsync(system, missingSlots.Count, cancellationToken);
        if (generated.Count == 0)
        {
            _logger.LogWarning("No usable baseline arguments were generated for {BeliefSystem}.", system.Slug);
            return;
        }

        for (var index = 0; index < generated.Count && index < missingSlots.Count; index++)
        {
            var sourceKey = $"{BuildSourcePrefix(system)}{missingSlots[index]}";
            var socialArgumentId = await GetOrCreateArgumentAsync(
                generated[index],
                system,
                serviceAccountId,
                sourceKey,
                cancellationToken);

            await analysis.AnalyzeSocialArgumentAsync(socialArgumentId, cancellationToken);
        }
    }

    private async Task<Dictionary<int, SocialArgument>> LoadExistingArgumentsAsync(
        CanonicalBeliefSystem system,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var prefix = BuildSourcePrefix(system);
        var existing = await db.SocialArguments
            .AsNoTracking()
            .Where(argument =>
                argument.GenerationSourceKey != null &&
                argument.GenerationSourceKey.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        return existing
            .Select(argument => new
            {
                Argument = argument,
                Slot = int.TryParse(argument.GenerationSourceKey![prefix.Length..], out var slot)
                    ? slot
                    : 0
            })
            .Where(item => item.Slot > 0)
            .GroupBy(item => item.Slot)
            .ToDictionary(group => group.Key, group => group.First().Argument);
    }

    private async Task<Guid> GetOrCreateArgumentAsync(
        GeneratedBaselineArgument generated,
        CanonicalBeliefSystem system,
        string serviceAccountId,
        string sourceKey,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.SocialArguments
            .AsNoTracking()
            .FirstOrDefaultAsync(argument => argument.GenerationSourceKey == sourceKey, cancellationToken);

        if (existing is not null)
            return existing.Id;

        var proposition = new SocialProposition
        {
            Text = generated.Claim,
            Type = SocialPropositionType.Claim,
            UserId = serviceAccountId,
            IsAIGenerated = true,
            IsConfirmed = false
        };
        var argument = new SocialArgument
        {
            Title = generated.Title,
            ClaimProposition = proposition,
            WarrantText = generated.Warrant,
            ResolutionText = generated.Resolution,
            UserId = serviceAccountId,
            IsPublic = true,
            IsAIGenerated = true,
            GenerationSourceKey = sourceKey,
            GeneratorProvider = "SemanticKernelFallbackChain",
            GeneratorModel = _configuration["AzureFoundry:ModelId"]
                ?? _configuration["Ollama:Model"]
                ?? "runtime-configured",
            GeneratorPromptVersion = BaselineContentGenerationService.PromptVersion,
            GenerationProvenanceJson = JsonSerializer.Serialize(new
            {
                beliefSystemId = system.Id,
                beliefSystemSlug = system.Slug,
                beliefSystemName = system.Name,
                generatedAtUtc = DateTime.UtcNow
            }),
            Tags = generated.Tags
                .Append(system.Slug)
                .Append("ai-generated")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        db.SocialArguments.Add(argument);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrentlyCreated = await db.SocialArguments
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.GenerationSourceKey == sourceKey, cancellationToken);
            if (concurrentlyCreated is not null)
                return concurrentlyCreated.Id;

            throw;
        }

        _logger.LogInformation(
            "Published AI baseline argument {ArgumentId} for {BeliefSystem} with source key {SourceKey}.",
            argument.Id,
            system.Slug,
            sourceKey);
        return argument.Id;
    }

    private static async Task<UserAccount> EnsureServiceAccountAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = await db.UserAccounts.FirstOrDefaultAsync(
            account => account.Username == ServiceAccountUsername,
            cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsServiceAccount)
            {
                throw new InvalidOperationException(
                    $"Username '{ServiceAccountUsername}' belongs to a human account and cannot be used for generation.");
            }

            return existing;
        }

        var account = new UserAccount
        {
            Username = ServiceAccountUsername,
            DisplayName = ServiceAccountDisplayName,
            PasswordHash = string.Empty,
            IsActive = true,
            IsServiceAccount = true
        };
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static string BuildSourcePrefix(CanonicalBeliefSystem system) =>
        $"{BaselineContentGenerationService.PromptVersion}:{system.Slug}:";
}