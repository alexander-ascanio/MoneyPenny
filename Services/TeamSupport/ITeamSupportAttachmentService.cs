namespace MoneyPenny.Services.TeamSupport;

public interface ITeamSupportAttachmentService
{
    IReadOnlyList<string> ExtractUrlsFromHtml(string? content);

    Task<IReadOnlyList<string>> DiscoverUrlsAsync(
        string? teamSupportActionId,
        string? teamSupportTicketId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamSupportAttachmentInfo>> ResolveAttachmentsAsync(
        string? teamSupportActionId,
        string? teamSupportTicketId,
        string? content,
        CancellationToken cancellationToken = default);

    Task<TeamSupportAttachmentDownload?> DownloadAsync(
        string url,
        CancellationToken cancellationToken = default);

    bool IsAllowedAttachmentUrl(string url);
}
