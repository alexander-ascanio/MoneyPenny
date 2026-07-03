using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Rag.Retrieval;

public interface IRetrievalService
{
    Task<float[]> CreateQueryEmbeddingAsync(
        string firstCommentText,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarDocumentChunk>> RetrieveSimilarFirstCommentsAsync(
        string firstCommentText,
        int excludeTicketId,
        bool knowledgeBaseOnly = false,
        double? minScoreOverride = null,
        bool allowFallbackToZero = true,
        float[]? queryVector = null,
        CancellationToken cancellationToken = default);
}
