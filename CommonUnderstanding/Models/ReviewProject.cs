namespace CommonUnderstanding.Models;

public enum ProjectRole { Owner, Editor, Reviewer, Reader }
public enum ProjectOperation
{
    Read, Contribute, Correct, Adjudicate, ManageMembership, ManagePolicy,
    Decide, Publish, Export, Delete
}

public sealed class ReviewProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = "1";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ProjectMember> Members { get; set; } = new();
}

public sealed class ProjectMember
{
    public Guid ProjectId { get; set; }
    public ReviewProject Project { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
}