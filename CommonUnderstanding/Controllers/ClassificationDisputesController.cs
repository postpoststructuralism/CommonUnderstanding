using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommonUnderstanding.Controllers;

[Authorize]
[ApiController]
[Route("api/classification-disputes")]
public sealed class ClassificationDisputesController : ControllerBase
{
    private readonly ClassificationDisputeService _disputes;

    public ClassificationDisputesController(ClassificationDisputeService disputes)
    {
        _disputes = disputes;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateClassificationDisputeRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var dispute = await _disputes.CreateAsync(
            userId,
            request.TargetType,
            request.TargetId,
            request.ClassificationType,
            request.Rationale,
            request.EvidenceStatement,
            request.SourceUrl,
            ct);
        return CreatedAtAction(nameof(Get), new { id = dispute.Id }, ToResponse(dispute));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dispute = await _disputes.GetAsync(id, ct);
        return dispute is null ? NotFound() : Ok(ToResponse(dispute));
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditHistory(
        [FromQuery] string targetType,
        [FromQuery] string targetId,
        CancellationToken ct)
    {
        return Ok(await _disputes.GetAuditHistoryAsync(targetType, targetId, ct));
    }

    [HttpPost("{id:guid}/evidence")]
    public async Task<IActionResult> AddEvidence(Guid id, AddDisputeEvidenceRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        return await _disputes.AddEvidenceAsync(id, userId, request.Statement, request.SourceUrl, ct)
            ? NoContent()
            : Conflict(new { error = "The dispute is missing or no longer accepts evidence." });
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, ResolveClassificationDisputeRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (!await _disputes.CanReviewAsync(userId, ct)) return Forbid();

        return await _disputes.ResolveAsync(id, userId, request.Resolution, request.Notes, ct)
            ? NoContent()
            : Conflict(new { error = "The dispute is missing or already closed." });
    }

    private static object ToResponse(ClassificationDispute dispute) => new
    {
        dispute.Id,
        dispute.TargetType,
        dispute.TargetId,
        dispute.ClassificationType,
        dispute.RaisedByUserId,
        dispute.Rationale,
        dispute.Status,
        dispute.Resolution,
        dispute.ReviewedByUserId,
        dispute.ResolutionNotes,
        dispute.ResolvedAt,
        dispute.CreatedAt,
        Evidence = dispute.Evidence.Select(item => new
        {
            item.Id,
            item.SubmittedByUserId,
            item.Statement,
            item.SourceUrl,
            item.CreatedAt
        })
    };
}

public sealed record CreateClassificationDisputeRequest(
    [property: Required, MaxLength(50)] string TargetType,
    [property: Required, MaxLength(100)] string TargetId,
    [property: Required, MaxLength(100)] string ClassificationType,
    [property: Required, MaxLength(2000)] string Rationale,
    [property: Required, MaxLength(4000)] string EvidenceStatement,
    [property: MaxLength(2000), Url] string? SourceUrl);

public sealed record AddDisputeEvidenceRequest(
    [property: Required, MaxLength(4000)] string Statement,
    [property: MaxLength(2000), Url] string? SourceUrl);

public sealed record ResolveClassificationDisputeRequest(
    DisputeResolution Resolution,
    [property: Required, MaxLength(2000)] string Notes);