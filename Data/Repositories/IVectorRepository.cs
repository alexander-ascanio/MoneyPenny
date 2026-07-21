using MoneyPenny.Models.Rag;

namespace MoneyPenny.Data.Repositories;

public interface IVectorRepository
{
    Task DeleteTicketIndexAsync(int ticketId, CancellationToken cancellationToken = default);
    Task DeleteChunksBySourceAsync(
        DocumentChunkSource source,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default);
    Task DeleteChunksByTicketAndSourceAsync(
        int ticketId,
        DocumentChunkSource source,
        CancellationToken cancellationToken = default);
    Task<bool> IsTicketIndexedAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<bool> IsTicketIndexedBySourceAsync(
        int ticketId,
        DocumentChunkSource source,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetIndexedTicketIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetIndexedTicketIdsBySourceAsync(
        DocumentChunkSource source,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default);
    Task<int> CountIndexedTicketsBySourceAsync(
        DocumentChunkSource source,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default);
    Task SaveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task SaveEmbeddingsAsync(IEnumerable<TicketEmbedding> embeddings, CancellationToken cancellationToken = default);
    void ClearChangeTracker();
    Task<IReadOnlyList<SimilarDocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        double minScore,
        int? ticketId = null,
        int? excludeTicketId = null,
        DocumentChunkSource? source = null,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetChunksByTicketAndSourceAsync(
        int ticketId,
        DocumentChunkSource source,
        CancellationToken cancellationToken = default);
    Task<RagQueryLog> SaveQueryLogAsync(
        RagQueryLog log,
        bool reuseIfUnrated = false,
        CancellationToken cancellationToken = default);
    Task<bool> RateQueryLogAsync(
        int queryLogId,
        string userId,
        short rating,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RagQueryLog>> GetRatedQueryLogsByTicketAsync(
        int ticketId,
        RagResponseType responseType,
        CancellationToken cancellationToken = default);

    Task<RagQueryLog?> GetLatestQueryLogByTicketAsync(
        int ticketId,
        RagResponseType responseType,
        CancellationToken cancellationToken = default);

    Task<bool> HasQueryLogForTicketAsync(
        int ticketId,
        CancellationToken cancellationToken = default);

    Task<int> CountRatedQueryLogsAsync(
        RagResponseType? responseType = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RagQueryLog>> GetRatedQueryLogsPageAsync(
        int skip,
        int take,
        RagResponseType? responseType = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RagRatingDailyStatsRow>> GetRatingDailyStatsAsync(
        RagResponseType? responseType = null,
        CancellationToken cancellationToken = default);

    Task UpdateQueryLogTeamSupportActionIdAsync(
        int queryLogId,
        string? teamSupportActionId,
        CancellationToken cancellationToken = default);
}
