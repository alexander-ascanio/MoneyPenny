using System.Text;
using MoneyPenny.Helpers;
using MoneyPenny.Options;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Ingestion;

public class CommentContentService : ICommentContentService
{
    private readonly IImageTextExtractionService _imageTextExtractionService;
    private readonly RagOptions _options;
    private readonly ILogger<CommentContentService> _logger;

    public CommentContentService(
        IImageTextExtractionService imageTextExtractionService,
        IOptions<RagOptions> options,
        ILogger<CommentContentService> logger)
    {
        _imageTextExtractionService = imageTextExtractionService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CommentIndexableContent> ToIndexableContentAsync(
        string? htmlContent,
        bool processImages = true,
        CancellationToken cancellationToken = default)
    {
        var plainText = TicketHtmlHelper.ToPlainText(htmlContent);
        var imageSources = TicketHtmlHelper.ExtractImageSources(htmlContent);

        if (!processImages || !_options.EnableImageTextExtraction || imageSources.Count == 0)
        {
            return new CommentIndexableContent
            {
                Text = plainText,
                ImagesDetected = imageSources.Count,
                ImagesExtracted = 0
            };
        }

        var extraction = await _imageTextExtractionService.ExtractAsync(imageSources, cancellationToken);
        if (extraction.Texts.Count == 0)
        {
            _logger.LogWarning(
                "Se detectaron {ImageCount} imagen(es) en el comentario pero no se pudo extraer texto. {Warning}",
                imageSources.Count,
                extraction.Warning);

            return new CommentIndexableContent
            {
                Text = plainText,
                ImagesDetected = imageSources.Count,
                ImagesExtracted = 0,
                ImageExtractionWarning = extraction.Warning
            };
        }

        var document = new StringBuilder(plainText);
        for (var i = 0; i < extraction.Texts.Count; i++)
        {
            document.AppendLine();
            document.AppendLine($"Texto extraído de imagen {i + 1}:");
            document.AppendLine(extraction.Texts[i]);
        }

        _logger.LogInformation(
            "Se añadió texto de {ImageCount} imagen(es) al contenido indexable.",
            extraction.Texts.Count);

        return new CommentIndexableContent
        {
            Text = document.ToString().Trim(),
            ImagesDetected = imageSources.Count,
            ImagesExtracted = extraction.Texts.Count,
            ImageExtractionWarning = extraction.Warning
        };
    }
}
