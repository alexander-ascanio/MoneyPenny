using MoneyPenny.Services.TeamSupport;

namespace MoneyPenny.Services.Ocr;

public class CommentImageOcrService : ICommentImageOcrService
{
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly ITesseractOcrService _ocrService;
    private readonly ILogger<CommentImageOcrService> _logger;

    public CommentImageOcrService(
        ITeamSupportAttachmentService attachmentService,
        ITesseractOcrService ocrService,
        ILogger<CommentImageOcrService> logger)
    {
        _attachmentService = attachmentService;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<CommentImageOcrResult> ExtractTextFromUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return CommentImageOcrResult.Fail("La URL de la imagen es obligatoria.");
        }

        if (!_attachmentService.IsAllowedAttachmentUrl(url))
        {
            return CommentImageOcrResult.Fail("La URL de la imagen no está permitida.");
        }

        var download = await _attachmentService.DownloadAsync(url, cancellationToken);
        if (download is null)
        {
            return CommentImageOcrResult.Fail(
                "No se pudo descargar la imagen. Comprueba las credenciales de TeamSupport.");
        }

        if (!download.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return CommentImageOcrResult.Fail("El adjunto no es una imagen.");
        }

        try
        {
            var text = await _ocrService.ExtractTextAsync(download.Content, cancellationToken);
            return CommentImageOcrResult.Ok(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer texto OCR de {Url}.", url);
            return CommentImageOcrResult.Fail("No se pudo extraer texto de la imagen con Tesseract.");
        }
    }
}
