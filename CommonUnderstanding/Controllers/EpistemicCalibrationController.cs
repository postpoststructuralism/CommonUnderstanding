using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommonUnderstanding.Controllers;

[ApiController]
[Route("api/calibration")]
[Produces("application/json")]
public sealed class EpistemicCalibrationController : ControllerBase
{
    private readonly CalibrationService _calibration;

    public EpistemicCalibrationController(CalibrationService calibration)
    {
        _calibration = calibration;
    }

    [HttpPost("predictions")]
    [Authorize]
    public async Task<IActionResult> CreatePrediction(
        CreatePredictionRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        try
        {
            var prediction = await _calibration.CreatePredictionAsync(
                userId,
                request.PropositionId,
                request.WorldviewId,
                request.Probability,
                request.ResolutionDate,
                ct);

            return Created($"/api/calibration/predictions/{prediction.Id}", ToResponse(prediction));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Proposition not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("predictions/{id:guid}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolvePrediction(
        Guid id,
        ResolvePredictionRequest request,
        CancellationToken ct)
    {
        try
        {
            return await _calibration.ResolvePredictionAsync(id, request.ActualOutcome, ct)
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("propositions/{id:int}/epistemic-status")]
    [Authorize]
    public async Task<IActionResult> SetEpistemicStatus(
        int id,
        SetEpistemicStatusRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        try
        {
            return await _calibration.SetEpistemicStatusAsync(
                id, userId, User.IsInRole("Admin"), request.Status, ct)
                ? NoContent()
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private static object ToResponse(Prediction prediction) => new
    {
        prediction.Id,
        prediction.PropositionId,
        prediction.UserId,
        prediction.WorldviewId,
        prediction.Probability,
        prediction.ResolutionDate,
        prediction.ActualOutcome,
        prediction.ResolvedAt,
        prediction.CreatedAt
    };
}

public sealed record CreatePredictionRequest(
    [property: Range(1, int.MaxValue)] int PropositionId,
    Guid? WorldviewId,
    [property: Range(0.0, 1.0)] double Probability,
    DateTime ResolutionDate);

public sealed record ResolvePredictionRequest(bool ActualOutcome);

public sealed record SetEpistemicStatusRequest(EpistemicStatus Status);