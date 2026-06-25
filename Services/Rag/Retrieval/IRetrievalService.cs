using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Rag.Retrieval;

public interface IRetrievalService
{
    Task<IReadOnlyList<SimilarDocumentChunk>> RetrieveContextAsync(
        string question,
        int? ticketId = null,
        CancellationToken cancellationToken = default);
}
