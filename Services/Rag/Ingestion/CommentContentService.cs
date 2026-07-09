using System.Text;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Options;
using MoneyPenny.Services.TeamSupport;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Ingestion;

public class CommentContentService : ICommentContentService
{
    private readonly IImageTextExtractionService _imageTextExtractionService;
    private readonly ICommentImageTextCacheRepository _imageTextCacheRepository;
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly RagOptions _options;
    private readonly ILogger<CommentContentService> _logger;

    public CommentContentService(
        IImageTextExtractionService imageTextExtractionService,
        ICommentImageTextCacheRepository imageTextCacheRepository,
        ITeamSupportAttachmentService attachmentService,
        IOptions<RagOptions> options,
        ILogger<CommentContentService> logger)
    {
        _imageTextExtractionService = imageTextExtractionService;
        _imageTextCacheRepository = imageTextCacheRepository;
        _attachmentService = attachmentService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CommentIndexableContent> ToIndexableContentAsync(
        string? htmlContent,
        CommentContentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new CommentContentRequest();
        var plainText = TicketHtmlHelper.ToPlainText(htmlContent);
        var imageSources = await ResolveDisplayableImageUrlsAsync(htmlContent, request, cancellationToken);

        if (!request.ProcessImages || !_options.EnableImageTextExtraction || imageSources.Count == 0)
        {
            return new CommentIndexableContent
            {
                Text = plainText,
                ImagesDetected = imageSources.Count,
                ImagesExtracted = 0
            };
        }

        var imageEntries = imageSources
            .Select(TicketHtmlHelper.SanitizeImageSource)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => new ImageSourceEntry(
                CommentImageSourceKey.ForCache(source),
                source))
            .GroupBy(entry => entry.CacheKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var cacheKeys = imageEntries.Select(entry => entry.CacheKey).ToArray();
        var cachedTexts = await _imageTextCacheRepository.GetByImageSourcesAsync(
            cacheKeys,
            cancellationToken);

        var textsByCacheKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in imageEntries)
        {
            if (cachedTexts.TryGetValue(entry.CacheKey, out var cachedText)
                && !string.IsNullOrWhiteSpace(cachedText))
            {
                textsByCacheKey[entry.CacheKey] = cachedText;
            }
        }

        var missingEntries = imageEntries
            .Where(entry => !textsByCacheKey.ContainsKey(entry.CacheKey))
            .ToArray();

        string? extractionWarning = null;
        if (missingEntries.Length > 0
            && request.ImageCacheMode == ImageExtractionCacheMode.UseAndRefresh)
        {
            var extraction = await _imageTextExtractionService.ExtractAsync(
                missingEntries.Select(entry => entry.Source).ToArray(),
                cancellationToken);
            extractionWarning = extraction.Warning;
            for (var i = 0; i < missingEntries.Length; i++)
            {
                var extractedText = i < extraction.Texts.Count ? extraction.Texts[i] : string.Empty;
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    continue;
                }

                var entry = missingEntries[i];
                textsByCacheKey[entry.CacheKey] = extractedText;

                if (request.TicketId is not null && request.TicketActionId is not null)
                {
                    await _imageTextCacheRepository.SaveAsync(
                        request.TicketId.Value,
                        request.TicketActionId.Value,
                        entry.CacheKey,
                        extractedText,
                        _options.VisionModel,
                        cancellationToken);
                }
            }

            if (textsByCacheKey.Count == 0 && !string.IsNullOrWhiteSpace(extraction.Warning))
            {
                return BuildResult(
                    plainText,
                    imageEntries,
                    textsByCacheKey,
                    extraction.Warning);
            }
        }

        string? warning = null;
        if (missingEntries.Length > 0 && request.ImageCacheMode == ImageExtractionCacheMode.CacheOnly)
        {
            warning = missingEntries.Length == imageEntries.Length
                ? "El texto de la imagen no está en caché. Indexa el ticket con 'Procesar con tokens' para extraerlo."
                : $"Faltan {missingEntries.Length} imagen(es) en caché. Indexa el ticket con 'Procesar con tokens' para extraerlas.";
        }

        if (!string.IsNullOrWhiteSpace(extractionWarning))
        {
            warning = string.IsNullOrWhiteSpace(warning)
                ? extractionWarning
                : $"{warning} {extractionWarning}";
        }

        if (textsByCacheKey.Count > 0)
        {
            _logger.LogInformation(
                "Texto de imagen: {CachedCount} desde caché, {VisionCount} vía Vision.",
                imageEntries.Count(entry => cachedTexts.ContainsKey(entry.CacheKey)),
                Math.Max(0, textsByCacheKey.Count - cachedTexts.Count));
        }

        return BuildResult(plainText, imageEntries, textsByCacheKey, warning);
    }

    private async Task<IReadOnlyList<string>> ResolveDisplayableImageUrlsAsync(
        string? htmlContent,
        CommentContentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TeamSupportActionId))
        {
            return CommentImageHelper.GetDisplayableImageUrls(htmlContent);
        }

        var attachments = await _attachmentService.ResolveAttachmentsAsync(
            request.TeamSupportActionId,
            request.TeamSupportTicketId,
            htmlContent,
            cancellationToken);

        var attachmentSources = attachments.Select(item => new CommentImageHelper.CommentImageSource(
            item.OriginalUrl,
            item.FileName,
            item.IsImage));

        return CommentImageHelper.GetDisplayableImageUrls(htmlContent, attachmentSources);
    }

    private static CommentIndexableContent BuildResult(
        string plainText,
        IReadOnlyList<ImageSourceEntry> imageEntries,
        IReadOnlyDictionary<string, string> textsByCacheKey,
        string? warning)
    {
        if (textsByCacheKey.Count == 0)
        {
            return new CommentIndexableContent
            {
                Text = plainText,
                ImagesDetected = imageEntries.Count,
                ImagesExtracted = 0,
                ImageExtractionWarning = warning
            };
        }

        var document = new StringBuilder(plainText);
        var imageIndex = 1;
        foreach (var entry in imageEntries)
        {
            if (!textsByCacheKey.TryGetValue(entry.CacheKey, out var extractedText))
            {
                continue;
            }

            document.AppendLine();
            document.AppendLine($"Texto extraído de imagen {imageIndex}:");
            document.AppendLine(TicketHtmlHelper.SanitizeForIndexing(extractedText));
            imageIndex++;
        }

        return new CommentIndexableContent
        {
            Text = document.ToString().Trim(),
            ImagesDetected = imageEntries.Count,
            ImagesExtracted = textsByCacheKey.Count,
            ImageExtractionWarning = warning
        };
    }

    private sealed record ImageSourceEntry(string CacheKey, string Source);
}
