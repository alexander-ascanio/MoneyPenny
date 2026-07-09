using MoneyPenny.Services.TeamSupport;

namespace MoneyPenny.Services.Cv;

public interface ICommentImageMessageBoxService
{
    Task<MessageBoxDetectionResult> DetectFromUrlAsync(string url, CancellationToken cancellationToken = default);

    Task<MessageBoxDetectionResult> DetectWithVisionFromUrlAsync(
        string url,
        CancellationToken cancellationToken = default);
}

public sealed class CommentImageMessageBoxService : ICommentImageMessageBoxService
{
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly IMessageBoxDetectionService _messageBoxDetectionService;

    public CommentImageMessageBoxService(
        ITeamSupportAttachmentService attachmentService,
        IMessageBoxDetectionService messageBoxDetectionService)
    {
        _attachmentService = attachmentService;
        _messageBoxDetectionService = messageBoxDetectionService;
    }

    public Task<MessageBoxDetectionResult> DetectFromUrlAsync(
        string url,
        CancellationToken cancellationToken = default) =>
        DetectFromUrlInternalAsync(url, MessageBoxTextEngine.Tesseract, cancellationToken);

    public Task<MessageBoxDetectionResult> DetectWithVisionFromUrlAsync(
        string url,
        CancellationToken cancellationToken = default) =>
        DetectFromUrlInternalAsync(url, MessageBoxTextEngine.Vision, cancellationToken);

    private async Task<MessageBoxDetectionResult> DetectFromUrlInternalAsync(
        string url,
        MessageBoxTextEngine textEngine,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return MessageBoxDetectionResult.Fail("La URL de la imagen es obligatoria.");
        }

        if (!_attachmentService.IsAllowedAttachmentUrl(url))
        {
            return MessageBoxDetectionResult.Fail("La URL de la imagen no está permitida.");
        }

        var download = await _attachmentService.DownloadAsync(url, cancellationToken);
        if (download is null)
        {
            return MessageBoxDetectionResult.Fail(
                "No se pudo descargar la imagen. Comprueba las credenciales de TeamSupport.");
        }

        if (!download.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return MessageBoxDetectionResult.Fail("El adjunto no es una imagen.");
        }

        return await _messageBoxDetectionService.DetectAsync(
            download.Content,
            textEngine,
            cancellationToken);
    }
}
