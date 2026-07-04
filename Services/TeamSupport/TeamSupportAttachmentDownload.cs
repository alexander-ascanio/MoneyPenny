namespace MoneyPenny.Services.TeamSupport;

public sealed class TeamSupportAttachmentDownload
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public string? FileName { get; init; }
}
