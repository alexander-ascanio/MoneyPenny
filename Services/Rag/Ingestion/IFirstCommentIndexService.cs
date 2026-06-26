namespace MoneyPenny.Services.Rag.Ingestion;

public interface IFirstCommentIndexService
{
    Task<FirstCommentIndexStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<FirstCommentIndexResult> IndexAllAsync(
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default);
    Task<FirstCommentIndexResult> IndexTicketAsync(
        string ticketNumber,
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default);
}
