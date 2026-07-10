namespace MoneyPenny.Services.TeamSupport;

public class TeamSupportActionCreateResult
{
    public bool Success { get; init; }
    public string? ActionId { get; init; }
    public string? ErrorMessage { get; init; }
}
