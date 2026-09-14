using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Provenance;

public interface ISourceTrustService
{
    Task<Source?> ResolveAsync(
        string? sourceName,
        string? sourceUri,
        string? publisherIdentifier = null,
        CancellationToken cancellationToken = default);
}

public sealed class SourceTrustService : ISourceTrustService
{
    private readonly ApplicationDbContext _db;

    public SourceTrustService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Source?> ResolveAsync(
        string? sourceName,
        string? sourceUri,
        string? publisherIdentifier = null,
        CancellationToken cancellationToken = default)
    {
        var domain = NormalizeDomain(sourceUri);
        sourceName = string.IsNullOrWhiteSpace(sourceName) ? domain : sourceName.Trim();
        publisherIdentifier = string.IsNullOrWhiteSpace(publisherIdentifier) ? null : publisherIdentifier.Trim();

        if (string.IsNullOrWhiteSpace(sourceName) && domain is null && publisherIdentifier is null)
            return null;

        var source = await _db.Sources.FirstOrDefaultAsync(
            candidate => (domain != null && candidate.Domain == domain)
                || (publisherIdentifier != null && candidate.PublisherIdentifier == publisherIdentifier),
            cancellationToken);

        if (source is not null)
            return source;

        source = new Source
        {
            Name = sourceName ?? publisherIdentifier ?? "Unknown source",
            Domain = domain,
            PublisherIdentifier = publisherIdentifier,
            ReliabilityScore = 0.5,
            VerificationStatus = SourceVerificationStatus.Unverified
        };
        _db.Sources.Add(source);
        await _db.SaveChangesAsync(cancellationToken);
        return source;
    }

    internal static string? NormalizeDomain(string? sourceUri)
    {
        if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri))
            return null;

        var host = uri.IdnHost.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }
}