using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Social;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace CommonUnderstanding.Services;

public sealed record ReferenceFrameworkImportRequest(
    string Name,
    string Description,
    ReferenceSourceType SourceType,
    string Version,
    string JurisdictionScope,
    bool IsShared,
    IReadOnlyCollection<string> OwnerUserIds,
    string ImportedByUserId);

public interface IReferenceFrameworkImportService
{
    Task<ReferenceFramework> ImportAsync(
        ReferenceFrameworkImportRequest request,
        IFormFile file,
        CancellationToken cancellationToken = default,
        Func<string, int, int, Task>? onProgress = null);
}

public class ReferenceFrameworkImportService : IReferenceFrameworkImportService
{
    private const long MaxFileSize = 20 * 1024 * 1024;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ArgumentDecompositionService _decompositionService;
    private readonly EmbeddingService _embeddingService;

    public ReferenceFrameworkImportService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ArgumentDecompositionService decompositionService,
        EmbeddingService embeddingService)
    {
        _dbFactory = dbFactory;
        _decompositionService = decompositionService;
        _embeddingService = embeddingService;
    }

    public async Task<ReferenceFramework> ImportAsync(
        ReferenceFrameworkImportRequest request,
        IFormFile file,
        CancellationToken cancellationToken = default,
        Func<string, int, int, Task>? onProgress = null)
    {
        const int preparationSteps = 4;
        Validate(request, file);
        var ownerIds = await ValidateOwnerIdsAsync(request, cancellationToken);
        if (onProgress != null) await onProgress("Validated document and import settings", 1, preparationSteps);

        var text = await ExtractTextAsync(file, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("The uploaded document did not contain extractable text.");

        var chunks = SplitIntoChunks(text);
        var totalSteps = preparationSteps + chunks.Count;
        if (onProgress != null)
            await onProgress($"Extracted {text.Length:N0} characters into {chunks.Count} analysis chunk{(chunks.Count == 1 ? string.Empty : "s")}", 2, totalSteps);

        var extracted = new Dictionary<string, (string Assessment, double Confidence)>(StringComparer.OrdinalIgnoreCase);
        for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            var currentChunk = chunkIndex + 1;
            var decomposition = await _decompositionService.DecomposeAsync(
                chunks[chunkIndex],
                onProgress: onProgress == null
                    ? null
                    : async (label, step, total) => await onProgress(
                        $"Chunk {currentChunk}/{chunks.Count}: {label}",
                        2 + chunkIndex,
                        totalSteps),
                cancellationToken: cancellationToken);
            AddProposition(extracted, decomposition.ClaimText, string.Empty, 0.5);

            for (var index = 0; index < decomposition.Premises.Count; index++)
            {
                var premise = decomposition.Premises[index];
                var assessment = decomposition.ProvisionalAssessments
                    .FirstOrDefault(item => string.Equals(item.PremiseText, premise, StringComparison.OrdinalIgnoreCase))
                    ?? decomposition.ProvisionalAssessments.ElementAtOrDefault(index);
                AddProposition(extracted, premise, assessment?.Assessment ?? string.Empty, assessment?.Confidence ?? 0.5);
            }

            if (onProgress != null)
                await onProgress(
                    $"Analyzed chunk {currentChunk}/{chunks.Count}; found {extracted.Count} unique propositions so far",
                    2 + currentChunk,
                    totalSteps);
        }

        var texts = extracted.Keys.ToList();
        if (onProgress != null)
            await onProgress($"Generating embeddings for {texts.Count} propositions", totalSteps - 1, totalSteps);
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (onProgress != null)
            await onProgress($"Saving framework for {ownerIds.Count} owner{(ownerIds.Count == 1 ? string.Empty : "s")}", totalSteps, totalSteps);

        var framework = new ReferenceFramework
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            SourceType = request.SourceType,
            Version = request.Version.Trim(),
            JurisdictionScope = request.JurisdictionScope.Trim(),
            IsShared = request.IsShared,
            SourceFileName = Path.GetFileName(file.FileName),
            SourceContentType = file.ContentType,
            SourceFileSize = file.Length,
            ImportedByUserId = request.ImportedByUserId,
            Owners = ownerIds.Select(userId => new ReferenceFrameworkOwner { UserId = userId }).ToList(),
            Propositions = texts.Select((propositionText, index) => new ReferenceProposition
            {
                Text = propositionText,
                Assessment = extracted[propositionText].Assessment,
                Confidence = extracted[propositionText].Confidence,
                SortOrder = index,
                Embedding = index < embeddings.Length ? embeddings[index] : null
            }).ToList()
        };

        db.ReferenceFrameworks.Add(framework);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return framework;
    }

    private async Task<List<string>> ValidateOwnerIdsAsync(
        ReferenceFrameworkImportRequest request,
        CancellationToken cancellationToken)
    {
        var importerId = request.ImportedByUserId.Trim();
        var requestedOwnerIds = request.OwnerUserIds
            .Append(importerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existingOwnerIds = await db.UserAccounts
            .Where(account => requestedOwnerIds.Contains(account.Id))
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);

        return ReconcileOwnerIds(importerId, requestedOwnerIds, existingOwnerIds);
    }

    internal static List<string> ReconcileOwnerIds(
        string importerId,
        IReadOnlyCollection<string> requestedOwnerIds,
        IReadOnlyCollection<string> existingOwnerIds)
    {
        if (!existingOwnerIds.Contains(importerId, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Your signed-in account no longer exists. Sign out and sign in again before importing a framework.");

        if (existingOwnerIds.Count != requestedOwnerIds.Count)
            throw new InvalidOperationException("One or more selected additional owners no longer exist. Remove them and try again.");

        return existingOwnerIds.ToList();
    }

    private static void Validate(ReferenceFrameworkImportRequest request, IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("A framework name is required.");
        if (string.IsNullOrWhiteSpace(request.Version)) throw new ArgumentException("A framework version is required.");
        if (string.IsNullOrWhiteSpace(request.ImportedByUserId)) throw new ArgumentException("An importing user is required.");
        if (file.Length == 0 || file.Length > MaxFileSize) throw new ArgumentException("The document must be between 1 byte and 20 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".txt" or ".html" or ".htm" or ".pdf"))
            throw new ArgumentException("Only PDF, HTML, and text documents are supported.");
    }

    private static async Task<string> ExtractTextAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(stream),
            ".html" or ".htm" => await ExtractHtmlAsync(stream, cancellationToken),
            _ => await ReadTextAsync(stream, cancellationToken)
        };
    }

    private static string ExtractPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private static async Task<string> ExtractHtmlAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var html = await reader.ReadToEndAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);
        document.DocumentNode.SelectNodes("//script|//style")?.ToList().ForEach(node => node.Remove());
        return WebUtility.HtmlDecode(document.DocumentNode.InnerText);
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    internal static IReadOnlyList<string> SplitIntoChunks(string text, int maxCharacters = 3_200)
    {
        var normalized = Regex.Replace(text.Replace("\r\n", "\n"), @"[ \t]+", " ").Trim();
        var paragraphs = Regex.Split(normalized, @"\n\s*\n").Where(value => !string.IsNullOrWhiteSpace(value));
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            foreach (var part in SplitOversizedParagraph(paragraph.Trim(), maxCharacters))
            {
                if (current.Length > 0 && current.Length + part.Length + 2 > maxCharacters)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }
                if (current.Length > 0) current.AppendLine().AppendLine();
                current.Append(part);
            }
        }

        if (current.Length > 0) chunks.Add(current.ToString());
        return chunks;
    }

    private static IEnumerable<string> SplitOversizedParagraph(string paragraph, int maxCharacters)
    {
        for (var offset = 0; offset < paragraph.Length; offset += maxCharacters)
            yield return paragraph.Substring(offset, Math.Min(maxCharacters, paragraph.Length - offset));
    }

    private static void AddProposition(
        IDictionary<string, (string Assessment, double Confidence)> propositions,
        string text,
        string assessment,
        double confidence)
    {
        text = Regex.Replace(text.Trim(), @"\s+", " ");
        if (!string.IsNullOrWhiteSpace(text)) propositions.TryAdd(text, (assessment, Math.Clamp(confidence, 0, 1)));
    }
}