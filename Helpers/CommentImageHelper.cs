using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Helpers;

public static class CommentImageHelper
{
    public sealed record CommentImageSource(string OriginalUrl, string? FileName, bool IsImage);

    public sealed record CommentImageItem(string OriginalUrl, string? FileName, string ProxyUrl);

    public static IReadOnlyList<string> GetDisplayableImageUrls(
        string? content,
        IEnumerable<CommentImageSource>? attachments = null)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Imágenes embebidas en el HTML del comentario (antes del pie de firma).
        // No aplicar el filtro UUID: TeamSupport suele nombrar capturas con GUID y sí se muestran en Details.
        foreach (var url in TicketHtmlHelper.ExtractImageSources(content))
        {
            urls.Add(url);
        }

        if (attachments is not null)
        {
            foreach (var attachment in attachments)
            {
                if (!attachment.IsImage
                    && !TicketHtmlHelper.IsLikelyImageAttachmentUrl(attachment.OriginalUrl))
                {
                    continue;
                }

                if (!TicketHtmlHelper.ShouldDisplayAsCommentAttachment(
                        attachment.OriginalUrl,
                        attachment.FileName))
                {
                    continue;
                }

                urls.Add(attachment.OriginalUrl);
            }
        }

        return urls.ToArray();
    }

    public static IReadOnlyList<CommentImageItem> GetCommentImages(
        string? content,
        IEnumerable<TicketAttachmentViewModel> attachments,
        Func<string, string> proxyUrlFactory)
    {
        var attachmentSources = attachments.Select(item => new CommentImageSource(
            item.OriginalUrl,
            item.FileName,
            item.IsImage));

        return GetDisplayableImageUrls(content, attachmentSources)
            .Select(url => new CommentImageItem(
                url,
                attachments.FirstOrDefault(item =>
                    string.Equals(item.OriginalUrl, url, StringComparison.OrdinalIgnoreCase))?.FileName
                    ?? TicketHtmlHelper.GuessAttachmentFileName(url),
                proxyUrlFactory(url)))
            .ToArray();
    }
}
