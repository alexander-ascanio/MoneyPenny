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
    private readonly ITicketRepository _ticketRepository;
    private readonly RagOptions _options;
    private readonly ILogger<PgVectorRetrievalService> _logger;

    public PgVectorRetrievalService(
        IEmbeddingService embeddingService,
        IVectorRepository vectorRepository,
        ITicketRepository ticketRepository,
        IOptions<RagOptions> options,
        ILogger<PgVectorRetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _vectorRepository = vectorRepository;
        _ticketRepository = ticketRepository;
        _options = options.Value;
        _logger = logger;
    }

    public Task<float[]> CreateQueryEmbeddingAsync(
        string firstCommentText,
        CancellationToken cancellationToken = default) =>
        _embeddingService.CreateEmbeddingAsync(firstCommentText, cancellationToken);

    public async Task<IReadOnlyList<SimilarDocumentChunk>> RetrieveSimilarFirstCommentsAsync(
        string firstCommentText,
        int excludeTicketId,
        bool knowledgeBaseOnly = false,
        double? minScoreOverride = null,
        float[]? queryVector = null,
        CancellationToken cancellationToken = default)
    {
        queryVector ??= await CreateQueryEmbeddingAsync(firstCommentText, cancellationToken);
        var minScore = minScoreOverride ?? _options.MinScore;
        var fetchLimit = knowledgeBaseOnly
            ? Math.Max(_options.TopK * 5, _options.TopK)
            : Math.Max(_options.TopK * 20, _options.TopK);
        var results = await SearchAndDedupeAsync(
            queryVector,
            fetchLimit,
            minScore,
            excludeTicketId,
            knowledgeBaseOnly,
            cancellationToken);

        _logger.LogInformation(
            "Recuperados {Count} ticket(s) similares por comentario #1 (excluyendo ticket {TicketId}, minScore={MinScore}, KB only={KnowledgeBaseOnly}).",
            results.Count,
            excludeTicketId,
            minScore,
            knowledgeBaseOnly);

        return results;
    }

    private async Task<IReadOnlyList<SimilarDocumentChunk>> SearchAndDedupeAsync(
        float[] queryVector,
        int fetchLimit,
        double minScore,
        int excludeTicketId,
        bool knowledgeBaseOnly,
        CancellationToken cancellationToken)
    {
        var raw = await _vectorRepository.SearchSimilarAsync(
            queryVector,
            fetchLimit,
            minScore,
            ticketId: null,
            excludeTicketId: excludeTicketId,
            source: DocumentChunkSource.ClientFirstComment,
            isKnowledgeBase: knowledgeBaseOnly,
            cancellationToken);

        if (!knowledgeBaseOnly)
        {
            var allowedTicketIds = await _ticketRepository.GetTicketIdsInNonKnowledgeBaseScopeAsync(
                raw.Select(item => item.Chunk.TicketId),
                cancellationToken);

            raw = raw
                .Where(item => allowedTicketIds.Contains(item.Chunk.TicketId))
                .ToList();
        }

        return raw
            .GroupBy(item => item.Chunk.TicketId)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(item => item.Score)
            .Take(_options.TopK)
            .ToList();
    }
}
