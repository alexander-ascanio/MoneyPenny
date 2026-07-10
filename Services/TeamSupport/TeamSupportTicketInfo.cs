namespace MoneyPenny.Services.TeamSupport;

public class TeamSupportTicketInfo
{
    public string? TicketNumber { get; init; }
    public string? Status { get; init; }
    public DateTime? CreatedAt { get; init; }
    public bool Found { get; init; }
    public string? ErrorMessage { get; init; }
}
