using System.Security.Claims;
using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class ReferenceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReferenceFrameworkImportService _importService;
    private readonly IReferenceFrameworkFitService _fitService;
    private readonly ILogger<ReferenceController> _logger;

    public ReferenceController(
        ApplicationDbContext db,
        IReferenceFrameworkImportService importService,
        IReferenceFrameworkFitService fitService,
        ILogger<ReferenceController> logger)
    {
        _db = db;
        _importService = importService;
        _fitService = fitService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return View(new ReferenceFrameworkIndexModel
        {
            Frameworks = await _fitService.GetAvailableFrameworksAsync(userId, cancellationToken),
            CurrentUserId = userId
        });
    }

    public IActionResult Import() => View(new ReferenceFrameworkImportModel());

    [HttpGet]
    public async Task<IActionResult> OwnerSearch(string? query, CancellationToken cancellationToken)
    {
        query = query?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Json(Array.Empty<ReferenceFrameworkOwnerOption>());

        var currentUserId = CurrentUserId();
        var normalizedQuery = query.ToLower();
        var users = await _db.UserAccounts
            .AsNoTracking()
            .Where(account => account.IsActive &&
                !account.IsServiceAccount &&
                account.Id != currentUserId &&
                (account.Username.Contains(normalizedQuery) || account.DisplayName.Contains(query)))
            .OrderBy(account => account.DisplayName)
            .ThenBy(account => account.Username)
            .Take(10)
            .Select(account => new ReferenceFrameworkOwnerOption(
                account.Id,
                account.Username,
                account.DisplayName))
            .ToListAsync(cancellationToken);

        return Json(users);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(ReferenceFrameworkImportModel model, CancellationToken cancellationToken)
    {
        var ownerIds = (model.AdditionalOwnerUserIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        model.AdditionalOwnerUserIds = ownerIds;

        if (!ModelState.IsValid || model.Document == null)
        {
            model.SelectedOwners = await GetOwnerOptionsAsync(ownerIds, cancellationToken);
            return View(model);
        }

        try
        {
            var framework = await _importService.ImportAsync(
                new ReferenceFrameworkImportRequest(
                    model.Name,
                    model.Description,
                    model.SourceType,
                    model.Version,
                    model.JurisdictionScope,
                    model.IsShared,
                    ownerIds,
                    CurrentUserId()),
                model.Document,
                cancellationToken);
            TempData["Success"] = $"Imported {framework.Propositions.Count} reference propositions.";
            return RedirectToAction(nameof(Details), new { id = framework.Id });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(model.Document), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Reference framework import failed for user {UserId}", CurrentUserId());
            ModelState.AddModelError(string.Empty, "The framework could not be imported. Please try again.");
        }

        model.SelectedOwners = await GetOwnerOptionsAsync(ownerIds, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task ImportStream(ReferenceFrameworkImportModel model)
    {
        var cancellationToken = HttpContext.RequestAborted;
        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        async Task SendAsync(object data)
        {
            await Response.WriteAsync(JsonSerializer.Serialize(data) + "\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        var ownerIds = (model.AdditionalOwnerUserIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!ModelState.IsValid || model.Document == null)
        {
            var errors = ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();
            await SendAsync(new { type = "error", message = errors.FirstOrDefault() ?? "Check the import fields and try again." });
            return;
        }

        try
        {
            var framework = await _importService.ImportAsync(
                new ReferenceFrameworkImportRequest(
                    model.Name,
                    model.Description,
                    model.SourceType,
                    model.Version,
                    model.JurisdictionScope,
                    model.IsShared,
                    ownerIds,
                    CurrentUserId()),
                model.Document,
                cancellationToken,
                async (label, step, total) => await SendAsync(new { type = "progress", label, step, total }));

            await SendAsync(new
            {
                type = "complete",
                label = $"Imported {framework.Propositions.Count} reference propositions",
                redirectUrl = Url.Action(nameof(Details), new { id = framework.Id })
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Reference framework import stream canceled for user {UserId}", CurrentUserId());
        }
        catch (ArgumentException exception)
        {
            await SendAsync(new { type = "error", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            await SendAsync(new { type = "error", message = exception.Message });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Reference framework import stream failed for user {UserId}", CurrentUserId());
            await SendAsync(new { type = "error", message = "The framework could not be imported. Please try again." });
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildDetailsModelAsync(id, null, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Score(
        Guid id,
        int? propositionId,
        Guid? socialArgumentId,
        CancellationToken cancellationToken)
    {
        if (propositionId.HasValue == socialArgumentId.HasValue)
        {
            TempData["Error"] = "Choose exactly one proposition or social argument to score.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var result = propositionId.HasValue
                ? await _fitService.ScorePropositionAsync(propositionId.Value, id, CurrentUserId(), cancellationToken)
                : await _fitService.ScoreSocialArgumentAsync(socialArgumentId!.Value, id, CurrentUserId(), cancellationToken);
            var model = await BuildDetailsModelAsync(id, result, cancellationToken);
            return model == null ? NotFound() : View(nameof(Details), model);
        }
        catch (KeyNotFoundException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Framework scoring failed for framework {FrameworkId}", id);
            TempData["Error"] = "The fit score could not be computed. Please try again.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ReferenceFrameworkDetailsModel?> BuildDetailsModelAsync(
        Guid id,
        ReferenceFrameworkFitResult? fitResult,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var framework = await _db.ReferenceFrameworks
            .AsNoTracking()
            .Include(item => item.Owners)
            .Include(item => item.Propositions.OrderBy(proposition => proposition.SortOrder))
            .FirstOrDefaultAsync(item => item.Id == id &&
                (item.IsShared || item.Owners.Any(owner => owner.UserId == userId)), cancellationToken);
        return framework == null
            ? null
            : new ReferenceFrameworkDetailsModel { Framework = framework, FitResult = fitResult };
    }

    private async Task<IReadOnlyList<ReferenceFrameworkOwnerOption>> GetOwnerOptionsAsync(
        IReadOnlyCollection<string> ownerIds,
        CancellationToken cancellationToken)
    {
        if (ownerIds.Count == 0) return [];

        return await _db.UserAccounts
            .AsNoTracking()
            .Where(account => ownerIds.Contains(account.Id) && account.IsActive && !account.IsServiceAccount)
            .OrderBy(account => account.DisplayName)
            .Select(account => new ReferenceFrameworkOwnerOption(
                account.Id,
                account.Username,
                account.DisplayName))
            .ToListAsync(cancellationToken);
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The authenticated user has no profile identifier.");
}
