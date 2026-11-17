using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

public sealed record ClaimRevisionInput(string Text, ClaimKind Kind, ClaimOrigin Origin,
    string Scope, string Attribution, Guid? SourceVersionId = null, int? PassageStart = null,
    int? PassageLength = null, string? PassageText = null, string Transformation = "Authored",
    EvidenceCoverage Coverage = EvidenceCoverage.Unknown, ClaimLifecycle Lifecycle = ClaimLifecycle.Active,
    string Objections = "");

public sealed record ClaimReviewRecord(Guid Id, ReviewDisposition Disposition, string ReviewerId,
    string Rationale, string Conditions, string ObjectionTreatment, string InputHash, string PolicyVersion, DateTimeOffset ReviewedAt);

public sealed record ProjectExportRecord(string FormatVersion, Guid ProjectId, string Name, string PolicyVersion,
    IReadOnlyList<ProjectClaimRecord> Claims, IReadOnlyList<ProjectSourceVersion> Sources);

public sealed record ProjectClaimRecord(Guid Id, Guid RevisionId, int Revision, string Text,
    ClaimKind Kind, ClaimOrigin Origin, string Scope, string Attribution, Guid? SourceVersionId,
    int? PassageStart, int? PassageLength, string? PassageText, string Transformation,
    EvidenceCoverage Coverage, ClaimLifecycle Lifecycle, bool RequiresRevalidation,
    ReviewDisposition Disposition, string? FormerStatus, string InputHash,
    IReadOnlyList<ClaimReviewRecord> Reviews, string Objections, Guid? PreviousRevisionId,
    string MatchingText, string CreatedBy, DateTimeOffset CreatedAt);

public sealed record ProjectChoice(string Value, string Label);
public sealed record ProjectChoices(IReadOnlyList<ProjectSourceVersion> Sources, IReadOnlyList<ProjectChoice> People,
    IReadOnlyList<ProjectChoice> Contributions, IReadOnlyList<ProjectChoice> Members);

public sealed class ProjectReviewService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<ProjectChoices> ChoicesAsync(Guid projectId, string actor, string query = "", CancellationToken ct = default,
        bool includeSources = true)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.Read, ct);
        var role = project.OwnerId == actor ? ProjectRole.Owner
            : (await db.ProjectMembers.SingleAsync(member => member.ProjectId == projectId && member.UserId == actor, ct)).Role;
        var sources = includeSources ? await db.ProjectSourceVersions.AsNoTracking().Where(source => source.ProjectId == projectId)
            .OrderBy(source => source.SourceKey).ThenByDescending(source => source.Version).ToListAsync(ct) : new List<ProjectSourceVersion>();
        var people = new List<ProjectChoice>();
        var members = new List<ProjectChoice>();
        var contributions = new List<ProjectChoice>();
        query = (query ?? "").Trim();
        if (query.Length > 100) throw new ArgumentException("Search is too long.");
        if (ProjectPermissions.Allows(role, ProjectOperation.ManageMembership))
        {
            members = await (from member in db.ProjectMembers
                join user in db.UserAccounts on member.UserId equals user.Id
                where member.ProjectId == projectId
                orderby user.Username
                select new ProjectChoice(user.Id, user.DisplayName + " @" + user.Username + " / " + member.Role)).ToListAsync(ct);
            if (query.Length >= 2)
                people = await db.UserAccounts.AsNoTracking().Where(user => user.IsActive && !user.IsServiceAccount
                    && user.Id != project.OwnerId && (user.Username.Contains(query) || user.DisplayName.Contains(query))).OrderBy(user => user.Username)
                    .Take(20).Select(user => new ProjectChoice(user.Id, user.DisplayName + " @" + user.Username)).ToListAsync(ct);
        }
        if (ProjectPermissions.Allows(role, ProjectOperation.Contribute))
            contributions = await db.Propositions.AsNoTracking().Where(item => item.Claim != null
                && item.Claim.Argument != null && item.Claim.Argument.SubmittedBy == actor && (query == "" || item.Text.Contains(query)))
                .OrderByDescending(item => item.Id).Take(50)
                .Select(item => new ProjectChoice(item.Id.ToString(), item.Claim!.Argument!.Title + ": " + item.Text)).ToListAsync(ct);
        return new(sources, people, contributions, members);
    }

    public async Task<ReviewProject> CreateAsync(string actor, string name, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireActorAsync(db, actor, ct);
        RequireText(name, 300);
        var project = new ReviewProject { Name = name.Trim(), OwnerId = actor };
        db.ReviewProjects.Add(project);
        await db.SaveChangesAsync(ct);
        return project;
    }

    public async Task<List<ReviewProject>> ListAsync(string actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireActorAsync(db, actor, ct);
        return await db.ReviewProjects.AsNoTracking().Where(project => project.OwnerId == actor
            || project.Members.Any(member => member.UserId == actor
                && (member.Role == ProjectRole.Editor || member.Role == ProjectRole.Reviewer || member.Role == ProjectRole.Reader)))
            .Where(project => db.UserAccounts.Any(user => user.Id == project.OwnerId && user.IsActive && !user.IsServiceAccount))
            .OrderBy(project => project.Name).ToListAsync(ct);
    }

    public async Task<ProjectRole> RoleAsync(Guid projectId, string actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.Read, ct);
        return project.OwnerId == actor ? ProjectRole.Owner
            : (await db.ProjectMembers.SingleAsync(member => member.ProjectId == projectId && member.UserId == actor, ct)).Role;
    }

    public async Task SetMemberAsync(Guid projectId, string actor, string memberId, ProjectRole? role, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.ManageMembership, ct);
        await RequireActorAsync(db, memberId, ct);
        if (memberId == project.OwnerId || role == ProjectRole.Owner || role.HasValue && !Enum.IsDefined(role.Value))
            throw new ArgumentException("Owner membership cannot be replaced; choose editor, reviewer, or reader.");
        var existing = await db.ProjectMembers.FindAsync(new object[] { projectId, memberId }, ct);
        if (role is null)
        {
            if (existing != null) db.ProjectMembers.Remove(existing);
        }
        else if (existing != null) existing.Role = role.Value;
        else db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = memberId, Role = role.Value });
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPolicyAsync(Guid projectId, string actor, string version, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.ManagePolicy, ct);
        RequireText(version, 100);
        project.PolicyVersion = version;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProjectSourceVersion> AddSourceAsync(Guid projectId, string actor, string key,
        string text, bool retracted = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await AuthorizeAsync(db, projectId, actor, ProjectOperation.Contribute, ct);
        RequireText(key, 300);
        RequireText(text, 1_000_000);
        var number = (await db.ProjectSourceVersions.Where(source => source.ProjectId == projectId && source.SourceKey == key)
            .Select(source => (int?)source.Version).MaxAsync(ct) ?? 0) + 1;
        var source = new ProjectSourceVersion { ProjectId = projectId, SourceKey = key, Version = number,
            Text = text, Sha256 = Hash(text), CreatedBy = actor, Retracted = retracted };
        db.ProjectSourceVersions.Add(source);
        await db.SaveChangesAsync(ct);
        return source;
    }

    public async Task<ProjectClaimRecord> SaveClaimAsync(Guid projectId, string actor, ClaimRevisionInput input,
        Guid? claimId = null, Guid? expectedRevisionId = null, CancellationToken ct = default)
        => await SaveRevisionAsync(projectId, actor, input, claimId, expectedRevisionId, null, ct);

    public async Task<ProjectClaimRecord> ImportLegacyAsync(Guid projectId, string actor, int propositionId,
        ClaimKind kind, string scope, string attribution, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await AuthorizeAsync(db, projectId, actor, ProjectOperation.Contribute, ct);
        var proposition = await db.Propositions.AsNoTracking().Include(item => item.Claim).ThenInclude(item => item!.Argument)
            .SingleOrDefaultAsync(item => item.Id == propositionId, ct) ?? throw new KeyNotFoundException();
        if (proposition.Claim?.Argument?.SubmittedBy != actor) throw new UnauthorizedAccessException();
        var input = new ClaimRevisionInput(proposition.Text, kind, ClaimOrigin.Unknown, scope, attribution,
            Transformation: $"Legacy proposition {propositionId}; original source provenance unavailable");
        return await SaveRevisionAsync(projectId, actor, input, null, null, proposition.Status.ToString(), ct);
    }

    private async Task<ProjectClaimRecord> SaveRevisionAsync(Guid projectId, string actor, ClaimRevisionInput input,
        Guid? claimId, Guid? expectedRevisionId, string? formerStatus, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await AuthorizeAsync(db, projectId, actor, claimId.HasValue ? ProjectOperation.Correct : ProjectOperation.Contribute, ct);
        RequireText(input.Text, 100_000);
        RequireText(input.Transformation, 1000);
        if (input.Scope == null || input.Attribution == null || input.Objections == null)
            throw new ArgumentException("Claim context must be supplied, even when unknown.");
        if (!Enum.IsDefined(input.Kind) || !Enum.IsDefined(input.Origin) || !Enum.IsDefined(input.Coverage) || !Enum.IsDefined(input.Lifecycle))
            throw new ArgumentException("Undefined claim dimension.");
        ProjectSourceVersion? source = null;
        if (input.SourceVersionId.HasValue)
        {
            source = await db.ProjectSourceVersions.SingleOrDefaultAsync(version => version.Id == input.SourceVersionId && version.ProjectId == projectId, ct)
                ?? throw new ArgumentException("Source version is not in this project.");
            if (input.PassageStart is not int start || input.PassageLength is not int length
                || start < 0 || length <= 0 || start > source.Text.Length - length
                || source.Text.Substring(start, length) != input.PassageText)
                throw new ArgumentException("Passage must match exact source coordinates.");
        }
        else if (input.PassageStart != null || input.PassageLength != null || input.PassageText != null)
            throw new ArgumentException("Passage coordinates require a source version.");
        if (input.Origin == ClaimOrigin.Imported && source is null)
            throw new ArgumentException("Imported claims require a source passage.");
        var sourceChanged = source != null && (source.Retracted
            || await db.ProjectSourceVersions.AnyAsync(version => version.ProjectId == projectId
                && version.SourceKey == source.SourceKey && version.Version > source.Version, ct));
        var claim = claimId.HasValue
            ? await db.ProjectClaims.Include(item => item.Revisions).SingleOrDefaultAsync(item => item.Id == claimId && item.ProjectId == projectId, ct)
                ?? throw new KeyNotFoundException()
            : new ProjectClaim { ProjectId = projectId };
        var previous = claim.Revisions.OrderByDescending(revision => revision.Number).FirstOrDefault();
        if (claimId.HasValue && previous?.Id != expectedRevisionId) throw new DbUpdateConcurrencyException("Claim revision changed.");
        var revision = new ProjectClaimRevision
        {
            ClaimId = claim.Id, Number = (previous?.Number ?? 0) + 1, DisplayText = input.Text,
            MatchingText = string.Join(' ', input.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant(),
            Kind = input.Kind, Origin = input.Origin, Scope = input.Scope, Attribution = input.Attribution,
            Objections = input.Objections,
            SourceVersionId = input.SourceVersionId, PassageStart = input.PassageStart, PassageLength = input.PassageLength,
            PassageText = input.PassageText, PreviousRevisionId = previous?.Id, Transformation = input.Transformation,
            CreatedBy = actor, Coverage = input.Coverage, Lifecycle = input.Lifecycle,
            FormerStatus = formerStatus,
            RequiresRevalidation = previous != null || sourceChanged,
            InputHash = Hash(JsonSerializer.Serialize(new { input, formerStatus }))
        };
        claim.ChangeToken = Guid.NewGuid();
        claim.Revisions.Add(revision);
        if (!claimId.HasValue) db.ProjectClaims.Add(claim);
        else db.ProjectClaimRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        return ToRecord(claim.Id, revision, sourceChanged: sourceChanged);
    }

    public async Task ReviewAsync(Guid projectId, Guid revisionId, string actor, ReviewDisposition disposition,
        string rationale, string conditions, string objectionTreatment, string expectedHash, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.Adjudicate, ct);
        RequireText(rationale, 10_000);
        if (conditions == null || objectionTreatment == null) throw new ArgumentException("Review context must be supplied.");
        if (!Enum.IsDefined(disposition) || disposition == ReviewDisposition.Unreviewed
            || disposition == ReviewDisposition.AcceptedWithObjections && string.IsNullOrWhiteSpace(objectionTreatment))
            throw new ArgumentException("Review requires a disposition and explicit objection treatment when applicable.");
        var claim = await db.ProjectClaims.Include(item => item.Revisions)
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.Revisions.Any(revision => revision.Id == revisionId), ct)
            ?? throw new KeyNotFoundException();
        var current = claim.Revisions.OrderByDescending(revision => revision.Number).First();
        if (current.Id != revisionId || current.InputHash != expectedHash)
            throw new DbUpdateConcurrencyException("Review inputs changed.");
        if (current.SourceVersionId is Guid sourceId)
        {
            var source = await db.ProjectSourceVersions.SingleAsync(item => item.Id == sourceId, ct);
            if (source.Retracted || await db.ProjectSourceVersions.AnyAsync(item => item.ProjectId == projectId
                && item.SourceKey == source.SourceKey && item.Version > source.Version, ct))
                throw new DbUpdateConcurrencyException("Source inputs changed; correct to the current source version before reviewing.");
        }
        if (disposition == ReviewDisposition.Accepted && !string.IsNullOrWhiteSpace(current.Objections))
            throw new ArgumentException("Use acceptance with objections and record their treatment.");
        claim.ChangeToken = Guid.NewGuid();
        db.ProjectClaimReviews.Add(new ProjectClaimReview { RevisionId = revisionId, Disposition = disposition,
            ReviewerId = actor, Rationale = rationale, Conditions = conditions, ObjectionTreatment = objectionTreatment,
            InputHash = current.InputHash, PolicyVersion = project.PolicyVersion });
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ProjectClaimRecord>> ReadClaimsAsync(Guid projectId, string actor,
        bool history = false, ProjectOperation operation = ProjectOperation.Read, CancellationToken ct = default)
    {
        if (operation is not (ProjectOperation.Read or ProjectOperation.Export)) throw new ArgumentException("Unsupported read operation.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, operation, ct);
        return await ReadRecordsAsync(db, project, history, ct);
    }

    private static async Task<List<ProjectClaimRecord>> ReadRecordsAsync(ApplicationDbContext db, ReviewProject project,
        bool history, CancellationToken ct)
    {
        var projectId = project.Id;
        var claims = await db.ProjectClaims.AsNoTracking().Include(claim => claim.Revisions).ThenInclude(revision => revision.Reviews)
            .Where(claim => claim.ProjectId == projectId).ToListAsync(ct);
        var sources = await db.ProjectSourceVersions.AsNoTracking().Where(source => source.ProjectId == projectId).ToListAsync(ct);
        return claims.SelectMany(claim => (history ? claim.Revisions : claim.Revisions.OrderByDescending(revision => revision.Number).Take(1))
            .Select(revision => ToRecord(claim.Id, revision, project.PolicyVersion,
                sources.Any(source => source.Id == revision.SourceVersionId && (source.Retracted
                    || sources.Any(newer => newer.SourceKey == source.SourceKey && newer.Version > source.Version)))))).ToList();
    }

    public async Task<ProjectExportRecord> ExportAsync(Guid projectId, string actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.Export, ct);
        var claims = await ReadRecordsAsync(db, project, true, ct);
        var sources = await db.ProjectSourceVersions.AsNoTracking().Where(source => source.ProjectId == projectId).ToListAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return new("project-review:1", projectId, project.Name, project.PolicyVersion, claims, sources);
    }

    public async Task DeleteAsync(Guid projectId, string actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var project = await AuthorizeAsync(db, projectId, actor, ProjectOperation.Delete, ct);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var claims = await db.ProjectClaims.Include(claim => claim.Revisions).ThenInclude(revision => revision.Reviews)
            .Where(claim => claim.ProjectId == projectId).ToListAsync(ct);
        foreach (var revision in claims.SelectMany(claim => claim.Revisions).OrderByDescending(revision => revision.Number))
        {
            db.ProjectClaimReviews.RemoveRange(revision.Reviews);
            db.ProjectClaimRevisions.Remove(revision);
            await db.SaveChangesAsync(ct);
        }
        db.ProjectClaims.RemoveRange(claims);
        db.ProjectSourceVersions.RemoveRange(await db.ProjectSourceVersions.Where(source => source.ProjectId == projectId).ToListAsync(ct));
        db.ProjectMembers.RemoveRange(await db.ProjectMembers.Where(member => member.ProjectId == projectId).ToListAsync(ct));
        db.ReviewProjects.Remove(project);
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
    }

    private static async Task<ReviewProject> AuthorizeAsync(ApplicationDbContext db, Guid id, string actor, ProjectOperation operation, CancellationToken ct)
    {
        await RequireActorAsync(db, actor, ct);
        var project = await db.ReviewProjects.SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw new KeyNotFoundException();
        if (string.IsNullOrWhiteSpace(project.OwnerId)) throw new UnauthorizedAccessException();
        await RequireActorAsync(db, project.OwnerId, ct);
        var role = project.OwnerId == actor ? ProjectRole.Owner
            : (await db.ProjectMembers.SingleOrDefaultAsync(member => member.ProjectId == id && member.UserId == actor, ct))?.Role;
        if (role is null || !ProjectPermissions.Allows(role.Value, operation)) throw new UnauthorizedAccessException();
        return project;
    }

    private static async Task RequireActorAsync(ApplicationDbContext db, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor) || !await db.UserAccounts.AnyAsync(user => user.Id == actor && user.IsActive && !user.IsServiceAccount, ct))
            throw new UnauthorizedAccessException();
    }

    private static ProjectClaimRecord ToRecord(Guid claimId, ProjectClaimRevision revision, string? policyVersion = null, bool sourceChanged = false)
    {
        var reviews = revision.Reviews.OrderBy(review => review.ReviewedAt).ToList();
        var applicable = reviews.LastOrDefault(review => review.InputHash == revision.InputHash
            && (policyVersion == null || review.PolicyVersion == policyVersion));
        var disposition = sourceChanged ? ReviewDisposition.Unreviewed : applicable?.Disposition ?? ReviewDisposition.Unreviewed;
        return new(claimId, revision.Id, revision.Number, revision.DisplayText, revision.Kind, revision.Origin,
            revision.Scope, revision.Attribution, revision.SourceVersionId, revision.PassageStart, revision.PassageLength,
            revision.PassageText, revision.Transformation, revision.Coverage, revision.Lifecycle,
            (revision.RequiresRevalidation || sourceChanged || reviews.Count > 0 && applicable == null) && disposition == ReviewDisposition.Unreviewed,
            disposition, revision.FormerStatus, revision.InputHash, reviews.Select(review => new ClaimReviewRecord(review.Id,
                review.Disposition, review.ReviewerId, review.Rationale, review.Conditions, review.ObjectionTreatment,
                review.InputHash, review.PolicyVersion, review.ReviewedAt)).ToList(), revision.Objections,
            revision.PreviousRevisionId, revision.MatchingText, revision.CreatedBy, revision.CreatedAt);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void RequireText(string text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum) throw new ArgumentException("Text is missing or too long.");
    }
}