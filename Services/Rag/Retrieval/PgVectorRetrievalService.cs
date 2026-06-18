using MoneyPenny.Data.Repositories;
using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Retrieval;

public class PgVectorRetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorRepository _vectorRepository;
    private readonly RagOptions _options;

    public PgVectorRetrievalService(
        IEmbeddingService embeddingService,
        IVectorRepository vectorRepository,
        IOptions<RagOptions> options)
    {
        _embeddingService = embeddingService;
        _vectorRepository = vectorRepository;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<DocumentChunk>> RetrieveContextAsync(
        string question,
        int? ticketId = null,
        CancellationToken cancellationToken = default)
    {
        var queryVector = await _embeddingService.CreateEmbeddingAsync(question, cancellationToken);
        var chunks = await _vectorRepository.SearchSimilarAsync(queryVector, _options.TopK, cancellationToken);

        if (ticketId is null)
        {
            return chunks;
        }

        return chunks.Where(c => c.TicketId == ticketId).ToList();
    }
}
