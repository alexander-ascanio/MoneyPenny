namespace MoneyPenny.Services.TeamSupport;

public sealed class TeamSupportAttachmentInfo
{
    public required string OriginalUrl { get; init; }
    public string? FileName { get; init; }
    public bool IsImage { get; init; }
}
