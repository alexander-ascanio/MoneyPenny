namespace MoneyPenny.Services.TeamSupport;

public class TeamSupportActionInfo
{
    public bool Found { get; init; }
    public string? ActionId { get; init; }
    public string? DescriptionHtml { get; init; }
    public string? CreatorName { get; init; }
    public DateTime? CreatedAt { get; init; }
    public bool IsPrivate { get; init; }
    public string? Source { get; init; }
    public string? ErrorMessage { get; init; }
}
