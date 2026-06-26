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

    public async Task<IReadOnlyList<SimilarDocumentChunk>> RetrieveSimilarFirstCommentsAsync(
        string firstCommentText,
        int excludeTicketId,
        CancellationToken cancellationToken = default)
    {
        var queryVector = await _embeddingService.CreateEmbeddingAsync(firstCommentText, cancellationToken);
        var fetchLimit = Math.Max(_options.TopK * 5, _options.TopK);
        var results = await SearchAndDedupeAsync(
            queryVector,
            fetchLimit,
            _options.MinScore,
            excludeTicketId,
            cancellationToken);

        if (results.Count == 0 && _options.MinScore > 0)
        {
            _logger.LogInformation(
                "Sin tickets similares para ticket {TicketId} con minScore={MinScore}. Reintentando sin umbral.",
                excludeTicketId,
                _options.MinScore);

            results = await SearchAndDedupeAsync(
                queryVector,
                fetchLimit,
                minScore: 0,
                excludeTicketId,
                cancellationToken);
        }

        _logger.LogInformation(
            "Recuperados {Count} ticket(s) similares por comentario #1 (excluyendo ticket {TicketId}, minScore={MinScore}).",
            results.Count,
            excludeTicketId,
            _options.MinScore);

        return results;
    }

    private async Task<IReadOnlyList<SimilarDocumentChunk>> SearchAndDedupeAsync(
        float[] queryVector,
        int fetchLimit,
        double minScore,
        int excludeTicketId,
        CancellationToken cancellationToken)
    {
        var raw = await _vectorRepository.SearchSimilarAsync(
            queryVector,
            fetchLimit,
            minScore,
            ticketId: null,
            excludeTicketId: excludeTicketId,
            source: DocumentChunkSource.ClientFirstComment,
            cancellationToken);

        return raw
            .GroupBy(item => item.Chunk.TicketId)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(item => item.Score)
            .Take(_options.TopK)
            .ToList();
    }
}
