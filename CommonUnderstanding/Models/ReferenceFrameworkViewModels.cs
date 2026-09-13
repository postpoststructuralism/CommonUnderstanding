using System.ComponentModel.DataAnnotations;
using CommonUnderstanding.Services;
using Microsoft.AspNetCore.Http;

namespace CommonUnderstanding.Models;

public class ReferenceFrameworkImportModel
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2_000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ReferenceSourceType SourceType { get; set; }

    [Required, MaxLength(100)]
    public string Version { get; set; } = string.Empty;

    [MaxLength(200)]
    public string JurisdictionScope { get; set; } = string.Empty;

    public bool IsShared { get; set; }

    public List<string>? AdditionalOwnerUserIds { get; set; }

    public IReadOnlyList<ReferenceFrameworkOwnerOption> SelectedOwners { get; set; } = [];

    [Required]
    public IFormFile? Document { get; set; }
}

public sealed record ReferenceFrameworkOwnerOption(
    string Id,
    string Username,
    string DisplayName);

public class ReferenceFrameworkIndexModel
{
    public IReadOnlyList<ReferenceFramework> Frameworks { get; init; } = [];
    public string CurrentUserId { get; init; } = string.Empty;
}

public class ReferenceFrameworkDetailsModel
{
    public ReferenceFramework Framework { get; init; } = null!;
    public ReferenceFrameworkFitResult? FitResult { get; init; }
}