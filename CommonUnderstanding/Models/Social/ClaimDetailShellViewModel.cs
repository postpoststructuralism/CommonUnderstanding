namespace CommonUnderstanding.Models.Social;

public sealed class ClaimDetailShellViewModel
{
    public required Guid ArgumentId { get; init; }
    public required string Title { get; init; }
    public required string PropositionText { get; init; }
    public string? Summary { get; init; }
    public DateTime UpdatedAt { get; init; }
}