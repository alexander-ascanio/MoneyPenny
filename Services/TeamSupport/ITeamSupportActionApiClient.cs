namespace MoneyPenny.Services.TeamSupport;

public interface ITeamSupportActionApiClient
{
    Task<TeamSupportActionCreateResult> CreatePrivateCommentAsync(
        string teamSupportTicketId,
        string commentHtml,
        string? creatorName = null,
        CancellationToken cancellationToken = default);

    Task<TeamSupportActionInfo> GetTicketActionAsync(
        string teamSupportTicketId,
        string teamSupportActionId,
        CancellationToken cancellationToken = default);
}
