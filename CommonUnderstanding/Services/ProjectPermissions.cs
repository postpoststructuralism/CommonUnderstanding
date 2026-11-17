using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

public static class ProjectPermissions
{
    public static bool Allows(ProjectRole role, ProjectOperation operation) =>
        Enum.IsDefined(role) && Enum.IsDefined(operation) && (role switch
        {
            ProjectRole.Owner => true,
            ProjectRole.Editor => operation is ProjectOperation.Read or ProjectOperation.Contribute or ProjectOperation.Correct,
            ProjectRole.Reviewer => operation is ProjectOperation.Read or ProjectOperation.Adjudicate,
            ProjectRole.Reader => operation == ProjectOperation.Read,
            _ => false
        });
}