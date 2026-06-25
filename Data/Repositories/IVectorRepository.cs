using MoneyPenny.Models.Rag;

namespace MoneyPenny.Data.Repositories;

public interface IVectorRepository
{
    Task<bool> IsTicketIndexedAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetIndexedTicketIdsAsync(CancellationToken cancellationToken = default);
    Task SaveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task SaveEmbeddingsAsync(IEnumerable<TicketEmbedding> embeddings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimilarDocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        double minScore,
        int? ticketId = null,
        CancellationToken cancellationToken = default);
    Task SaveQueryLogAsync(RagQueryLog log, CancellationToken cancellationToken = default);
}
