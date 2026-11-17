using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Controllers;

public sealed record CreateReviewProjectRequest(string Name);
public sealed record ProjectMemberRequest(string UserId, ProjectRole? Role);
public sealed record ProjectPolicyRequest(string Version);
public sealed record ProjectSourceRequest(string Key, string Text, bool Retracted = false);
public sealed record ProjectLegacyRequest(int PropositionId, ClaimKind Kind, string Scope, string Attribution);
public sealed record ProjectCorrectionRequest(ClaimRevisionInput Claim, Guid? ExpectedRevisionId);
public sealed record ProjectReviewRequest(ReviewDisposition Disposition, string Rationale, string Conditions,
    string ObjectionTreatment, string ExpectedHash);

[Authorize]
public sealed class ProjectsController(ProjectReviewService service) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("/Projects")]
    public Task<IActionResult> Index(CancellationToken ct) => ExecuteAsync(async () => View(await service.ListAsync(Actor, ct)));

    [HttpGet("/Projects/{id:guid}")]
    public Task<IActionResult> Details(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var claims = await service.ReadClaimsAsync(id, Actor, true, ct: ct);
        var project = (await service.ListAsync(Actor, ct)).Single(item => item.Id == id);
        ViewData["Project"] = project;
        ViewData["Role"] = await service.RoleAsync(id, Actor, ct);
        ViewData["Choices"] = await service.ChoicesAsync(id, Actor, ct: ct);
        return View(claims);
    });

    [HttpGet("/api/projects")]
    public Task<IActionResult> List(CancellationToken ct) => ExecuteAsync(async () => Ok(await service.ListAsync(Actor, ct)));

    [HttpGet("/api/projects/{id:guid}/choices")]
    public Task<IActionResult> Choices(Guid id, string query = "", CancellationToken ct = default) => ExecuteAsync(async () =>
        Ok(await service.ChoicesAsync(id, Actor, query, ct, includeSources: false)));

    [HttpPost("/api/projects"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create([FromBody] CreateReviewProjectRequest request, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var project = await service.CreateAsync(Actor, request.Name, ct);
        return Created($"/Projects/{project.Id}", project);
    });

    [HttpPost("/api/projects/{id:guid}/members"), ValidateAntiForgeryToken]
    public Task<IActionResult> Member(Guid id, [FromBody] ProjectMemberRequest request, CancellationToken ct) => ExecuteAsync(async () =>
    {
        await service.SetMemberAsync(id, Actor, request.UserId, request.Role, ct);
        return NoContent();
    });

    [HttpPost("/api/projects/{id:guid}/policy"), ValidateAntiForgeryToken]
    public Task<IActionResult> Policy(Guid id, [FromBody] ProjectPolicyRequest request, CancellationToken ct) => ExecuteAsync(async () =>
    {
        await service.SetPolicyAsync(id, Actor, request.Version, ct);
        return NoContent();
    });

    [HttpPost("/api/projects/{id:guid}/sources"), ValidateAntiForgeryToken]
    public Task<IActionResult> Source(Guid id, [FromBody] ProjectSourceRequest request, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var source = await service.AddSourceAsync(id, Actor, request.Key, request.Text, request.Retracted, ct);
        return Created($"/api/projects/{id}/sources/{source.Id}", source);
    });

    [HttpPost("/api/projects/{id:guid}/legacy"), ValidateAntiForgeryToken]
    public Task<IActionResult> ImportLegacy(Guid id, [FromBody] ProjectLegacyRequest request, CancellationToken ct) => ExecuteAsync(async () =>
        Ok(await service.ImportLegacyAsync(id, Actor, request.PropositionId, request.Kind, request.Scope, request.Attribution, ct)));

    [HttpGet("/api/projects/{id:guid}/claims")]
    public Task<IActionResult> Claims(Guid id, bool history, CancellationToken ct) => ExecuteAsync(async () =>
        Ok(await service.ReadClaimsAsync(id, Actor, history, ct: ct)));

    [HttpGet("/api/projects/{id:guid}/claims/{claimId:guid}")]
    public Task<IActionResult> Claim(Guid id, Guid claimId, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var history = (await service.ReadClaimsAsync(id, Actor, true, ct: ct)).Where(item => item.Id == claimId).ToList();
        return history.Count == 0 ? NotFound() : Ok(history);
    });

    [HttpPost("/api/projects/{id:guid}/claims"), ValidateAntiForgeryToken]
    public Task<IActionResult> AddClaim(Guid id, [FromBody] ClaimRevisionInput request, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var claim = await service.SaveClaimAsync(id, Actor, request, ct: ct);
        return Created($"/api/projects/{id}/claims/{claim.Id}", claim);
    });

    [HttpPost("/api/projects/{id:guid}/claims/{claimId:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Correct(Guid id, Guid claimId, [FromBody] ProjectCorrectionRequest request, CancellationToken ct) => ExecuteAsync(async () =>
        Ok(await service.SaveClaimAsync(id, Actor, request.Claim, claimId, request.ExpectedRevisionId, ct)));

    [HttpPost("/api/projects/{id:guid}/reviews/{revisionId:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Review(Guid id, Guid revisionId, [FromBody] ProjectReviewRequest request, CancellationToken ct) => ExecuteAsync(async () =>
    {
        await service.ReviewAsync(id, revisionId, Actor, request.Disposition, request.Rationale,
            request.Conditions, request.ObjectionTreatment, request.ExpectedHash, ct);
        return NoContent();
    });

    [HttpGet("/api/projects/{id:guid}/export")]
    public Task<IActionResult> Export(Guid id, CancellationToken ct) => ExecuteAsync(async () => Ok(await service.ExportAsync(id, Actor, ct)));

    [HttpPost("/api/projects/{id:guid}/delete"), ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
    {
        await service.DeleteAsync(id, Actor, ct);
        return NoContent();
    });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { return await action(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateConcurrencyException) { return Problem(statusCode: 409, title: "Inputs changed. Reload before continuing."); }
        catch (ArgumentException exception) { return Problem(statusCode: 400, title: exception.Message); }
    }
}