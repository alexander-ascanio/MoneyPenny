using System.Text;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Options;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Ingestion;

public class CommentContentService : ICommentContentService
{
    private readonly IImageTextExtractionService _imageTextExtractionService;
    private readonly ICommentImageTextCacheRepository _imageTextCacheRepository;
    private readonly RagOptions _options;
    private readonly ILogger<CommentContentService> _logger;

    public CommentContentService(
        IImageTextExtractionService imageTextExtractionService,
        ICommentImageTextCacheRepository imageTextCacheRepository,
        IOptions<RagOptions> options,
        ILogger<CommentContentService> logger)
    {
        _imageTextExtractionService = imageTextExtractionService;
        _imageTextCacheRepository = imageTextCacheRepository;
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
        var imageSources = TicketHtmlHelper.ExtractImageSources(htmlContent);

        if (!request.ProcessImages || !_options.EnableImageTextExtraction || imageSources.Count == 0)
        {
            return new CommentIndexableContent
            {
                Text = plainText,
                ImagesDetected = imageSources.Count,
                ImagesExtracted = 0
            };
        }

        var normalizedSources = imageSources
            .Select(TicketHtmlHelper.SanitizeImageSource)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .ToArray();

        var cachedTexts = await _imageTextCacheRepository.GetByImageSourcesAsync(
            normalizedSources,
            cancellationToken);

        var textsBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in normalizedSources)
        {
            if (cachedTexts.TryGetValue(source, out var cachedText)
                && !string.IsNullOrWhiteSpace(cachedText))
            {
                textsBySource[source] = cachedText;
            }
        }

        var missingSources = normalizedSources
            .Where(source => !textsBySource.ContainsKey(source))
            .ToArray();

        if (missingSources.Length > 0
            && request.ImageCacheMode == ImageExtractionCacheMode.UseAndRefresh)
        {
            var extraction = await _imageTextExtractionService.ExtractAsync(missingSources, cancellationToken);
            for (var i = 0; i < missingSources.Length && i < extraction.Texts.Count; i++)
            {
                var source = missingSources[i];
                var extractedText = extraction.Texts[i];
                textsBySource[source] = extractedText;

                if (request.TicketId is not null && request.TicketActionId is not null)
                {
                    await _imageTextCacheRepository.SaveAsync(
                        request.TicketId.Value,
                        request.TicketActionId.Value,
                        source,
                        extractedText,
                        _options.VisionModel,
                        cancellationToken);
                }
            }

            if (extraction.Texts.Count == 0 && !string.IsNullOrWhiteSpace(extraction.Warning))
            {
                return BuildResult(
                    plainText,
                    normalizedSources,
                    textsBySource,
                    extraction.Warning);
            }
        }

        string? warning = null;
        if (missingSources.Length > 0 && request.ImageCacheMode == ImageExtractionCacheMode.CacheOnly)
        {
            warning = missingSources.Length == normalizedSources.Length
                ? "El texto de la imagen no está en caché. Indexa el ticket con 'Procesar con tokens' para extraerlo."
                : $"Faltan {missingSources.Length} imagen(es) en caché. Indexa el ticket con 'Procesar con tokens' para extraerlas.";
        }

        if (textsBySource.Count > 0)
        {
            _logger.LogInformation(
                "Texto de imagen: {CachedCount} desde caché, {VisionCount} vía Vision.",
                normalizedSources.Count(source => cachedTexts.ContainsKey(source)),
                Math.Max(0, textsBySource.Count - cachedTexts.Count));
        }

        return BuildResult(plainText, normalizedSources, textsBySource, warning);
    }

    private static CommentIndexableContent BuildResult(
        string plainText,
        IReadOnlyList<string> normalizedSources,
        IReadOnlyDictionary<string, string> textsBySource,
        string? warning)
    {
        if (textsBySource.Count == 0)
        {
            return new CommentIndexableContent
            {
                Text = plainText,
                ImagesDetected = normalizedSources.Count,
                ImagesExtracted = 0,
                ImageExtractionWarning = warning
            };
        }

        var document = new StringBuilder(plainText);
        var imageIndex = 1;
        foreach (var source in normalizedSources)
        {
            if (!textsBySource.TryGetValue(source, out var extractedText))
            {
                continue;
            }

            document.AppendLine();
            document.AppendLine($"Texto extraído de imagen {imageIndex}:");
            document.AppendLine(extractedText);
            imageIndex++;
        }

        return new CommentIndexableContent
        {
            Text = document.ToString().Trim(),
            ImagesDetected = normalizedSources.Count,
            ImagesExtracted = textsBySource.Count,
            ImageExtractionWarning = warning
        };
    }
}
