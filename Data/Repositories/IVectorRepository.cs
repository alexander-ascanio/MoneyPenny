using MoneyPenny.Models.Rag;

namespace MoneyPenny.Data.Repositories;

public interface IVectorRepository
{
    Task DeleteTicketIndexAsync(int ticketId, CancellationToken cancellationToken = default);
    Task DeleteChunksBySourceAsync(DocumentChunkSource source, CancellationToken cancellationToken = default);
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
        CancellationToken cancellationToken = default);
    Task<int> CountIndexedTicketsBySourceAsync(
        DocumentChunkSource source,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default);
    Task SaveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task SaveEmbeddingsAsync(IEnumerable<TicketEmbedding> embeddings, CancellationToken cancellationToken = default);
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
    Task SaveQueryLogAsync(RagQueryLog log, CancellationToken cancellationToken = default);
}
