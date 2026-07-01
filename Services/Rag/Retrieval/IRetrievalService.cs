using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Rag.Retrieval;

public interface IRetrievalService
{
    Task<IReadOnlyList<SimilarDocumentChunk>> RetrieveSimilarFirstCommentsAsync(
        string firstCommentText,
        int excludeTicketId,
        bool knowledgeBaseOnly = false,
        CancellationToken cancellationToken = default);
}
