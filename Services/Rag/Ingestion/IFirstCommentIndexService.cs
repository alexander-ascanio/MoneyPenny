namespace MoneyPenny.Services.Rag.Ingestion;

using MoneyPenny.Models.Tickets;

public interface IFirstCommentIndexService
{
    Task<FirstCommentIndexCounts> GetCountsAsync(
        bool onlyTicketsListScope = true,
        CancellationToken cancellationToken = default);
    Task<FirstCommentCorpusStats> GetCorpusStatsAsync(
        int sampleSize = 200,
        bool onlyKnowledgeBaseScope = false,
        CancellationToken cancellationToken = default);
    Task<int> CountBulkTicketsToProcessAsync(
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default);
    Task<FirstCommentIndexResult> IndexAllAsync(
        FirstCommentIndexOptions options,
        IFirstCommentBulkIndexProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
    Task<FirstCommentIndexResult> IndexTicketAsync(
        string ticketNumber,
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default);
}
