using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Rag.Retrieval;

public interface IRetrievalService
{
    Task<IReadOnlyList<DocumentChunk>> RetrieveContextAsync(string question, int? ticketId = null, CancellationToken cancellationToken = default);
}
