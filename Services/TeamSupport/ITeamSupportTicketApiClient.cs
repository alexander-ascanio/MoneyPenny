namespace MoneyPenny.Services.TeamSupport;

public interface ITeamSupportTicketApiClient
{
    Task<TeamSupportTicketInfo> GetTicketAsync(
        string teamSupportTicketId,
        CancellationToken cancellationToken = default);
}
