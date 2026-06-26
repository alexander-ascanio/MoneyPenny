namespace MoneyPenny.Services.Rag.Ingestion;

using MoneyPenny.Models.Tickets;

public interface IFirstCommentIndexService
{
    Task<FirstCommentIndexCounts> GetCountsAsync(CancellationToken cancellationToken = default);
    Task<FirstCommentCorpusStats> GetCorpusStatsAsync(
        int sampleSize = 200,
        CancellationToken cancellationToken = default);
    Task<FirstCommentIndexResult> IndexAllAsync(
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default);
    Task<FirstCommentIndexResult> IndexTicketAsync(
        string ticketNumber,
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default);
}
