using MoneyPenny.Data.Repositories;
using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Retrieval;

public class PgVectorRetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorRepository _vectorRepository;
    private readonly RagOptions _options;
    private readonly ILogger<PgVectorRetrievalService> _logger;

    public PgVectorRetrievalService(
        IEmbeddingService embeddingService,
        IVectorRepository vectorRepository,
        IOptions<RagOptions> options,
        ILogger<PgVectorRetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _vectorRepository = vectorRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SimilarDocumentChunk>> RetrieveContextAsync(
        string question,
        int? ticketId = null,
        CancellationToken cancellationToken = default)
    {
        var queryVector = await _embeddingService.CreateEmbeddingAsync(question, cancellationToken);
        var results = await _vectorRepository.SearchSimilarAsync(
            queryVector,
            _options.TopK,
            _options.MinScore,
            ticketId,
            cancellationToken);

        _logger.LogInformation(
            "Recuperados {Count} chunks para la pregunta (ticketId={TicketId}, minScore={MinScore}).",
            results.Count,
            ticketId,
            _options.MinScore);

        return results;
    }
}
