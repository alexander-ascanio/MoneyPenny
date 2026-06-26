using System.Text;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Rag;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using MoneyPenny.Services.Rag.Pricing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentIndexService : IFirstCommentIndexService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IVectorRepository _vectorRepository;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ICommentContentService _commentContentService;
    private readonly IRagTokenEstimateService _tokenEstimateService;
    private readonly RagOptions _options;
    private readonly ILogger<FirstCommentIndexService> _logger;

    public FirstCommentIndexService(
        ITicketRepository ticketRepository,
        IVectorRepository vectorRepository,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        ICommentContentService commentContentService,
        IRagTokenEstimateService tokenEstimateService,
        IOptions<RagOptions> options,
        ILogger<FirstCommentIndexService> logger)
    {
        _ticketRepository = ticketRepository;
        _vectorRepository = vectorRepository;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _commentContentService = commentContentService;
        _tokenEstimateService = tokenEstimateService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FirstCommentIndexStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var total = await _ticketRepository.CountTicketsWithFirstCommentAsync(cancellationToken);
        var indexed = await _vectorRepository.CountIndexedTicketsBySourceAsync(
            DocumentChunkSource.ClientFirstComment,
            cancellationToken);
        var corpus = await _ticketRepository.GetFirstCommentCorpusStatsAsync(200, cancellationToken);

        return new FirstCommentIndexStatus
        {
            TotalTicketsWithFirstComment = total,
            IndexedTickets = indexed,
            AverageCommentCharCount = corpus.AverageCharCount,
            AverageImagesPerTicket = corpus.AverageImagesPerTicket,
            CorpusSampleSize = corpus.SampleSize
        };
    }

    public async Task<FirstCommentIndexResult> IndexAllAsync(
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.RebuildAll)
        {
            _logger.LogInformation("Eliminando índice masivo de comentarios #1 existente.");
            await _vectorRepository.DeleteChunksBySourceAsync(
                DocumentChunkSource.ClientFirstComment,
                cancellationToken);
        }

        var alreadyIndexed = options.SkipAlreadyIndexed && !options.RebuildAll
            ? (await _vectorRepository.GetIndexedTicketIdsBySourceAsync(
                DocumentChunkSource.ClientFirstComment,
                cancellationToken)).ToHashSet()
            : [];

        var batchSize = Math.Max(1, _options.FirstCommentIndexBatchSize);
        var skip = 0;
        var processed = 0;
        var indexed = 0;
        var skipped = 0;
        var failed = 0;
        var chunksCreated = 0;
        var embeddingsCreated = 0;
        var errors = new List<string>();
        var usageParts = new List<TokenUsageEstimate>();
        var maxTickets = options.MaxTickets;

        while (true)
        {
            if (maxTickets is > 0 && processed >= maxTickets.Value)
            {
                break;
            }

            var take = maxTickets is > 0
                ? Math.Min(batchSize, maxTickets.Value - processed)
                : batchSize;

            var page = await _ticketRepository.GetFirstCommentsPageAsync(skip, take, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            foreach (var row in page)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (maxTickets is > 0 && processed >= maxTickets.Value)
                {
                    break;
                }

                processed++;

                if (alreadyIndexed.Contains(row.TicketId))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var ticketChunks = await IndexFirstCommentRowAsync(row, options.ProcessImages, cancellationToken);
                    if (ticketChunks.Chunks == 0)
                    {
                        skipped++;
                        continue;
                    }

                    indexed++;
                    chunksCreated += ticketChunks.Chunks;
                    embeddingsCreated += ticketChunks.Embeddings;
                    if (ticketChunks.Usage is not null)
                    {
                        usageParts.Add(ticketChunks.Usage);
                    }

                    if (options.ProcessImages
                        && ticketChunks.ImagesDetected > 0
                        && ticketChunks.ImagesExtracted == 0
                        && !string.IsNullOrWhiteSpace(ticketChunks.ImageExtractionWarning))
                    {
                        errors.Add(
                            $"Ticket #{row.TicketNumber}: {ticketChunks.ImageExtractionWarning}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    var message = $"Ticket #{row.TicketNumber} (Id={row.TicketId}): {ex.Message}";
                    errors.Add(message);
                    _logger.LogError(ex, "Error indexando comentario #1 del ticket {TicketId}.", row.TicketId);
                }
            }

            skip += page.Count;

            if (page.Count < take)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Indexación masiva de comentarios #1 finalizada: processed={Processed}, indexed={Indexed}, skipped={Skipped}, failed={Failed}, chunks={Chunks}.",
            processed,
            indexed,
            skipped,
            failed,
            chunksCreated);

        return new FirstCommentIndexResult
        {
            TicketsProcessed = processed,
            TicketsIndexed = indexed,
            TicketsSkipped = skipped,
            TicketsFailed = failed,
            ChunksCreated = chunksCreated,
            EmbeddingsCreated = embeddingsCreated,
            UsageEstimate = usageParts.Count > 0 ? _tokenEstimateService.Combine(usageParts.ToArray()) : null,
            Errors = errors
        };
    }

    public async Task<FirstCommentIndexResult> IndexTicketAsync(
        string ticketNumber,
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            return SingleTicketError("Indica un número de ticket.");
        }

        var row = await _ticketRepository.GetFirstCommentByTicketNumberAsync(ticketNumber, cancellationToken);
        if (row is null)
        {
            var normalized = ticketNumber.Trim().TrimStart('#');
            var ticket = await _ticketRepository.GetByNumberAsync(normalized, cancellationToken);
            if (ticket is null)
            {
                return SingleTicketError($"No se encontró el ticket #{normalized}.");
            }

            return SingleTicketError($"El ticket #{normalized} no tiene comentario #1 con contenido indexable.");
        }

        if (options.SkipAlreadyIndexed)
        {
            var alreadyIndexed = await _vectorRepository.IsTicketIndexedBySourceAsync(
                row.TicketId,
                DocumentChunkSource.ClientFirstComment,
                cancellationToken);
            if (alreadyIndexed)
            {
                return new FirstCommentIndexResult
                {
                    TicketsProcessed = 1,
                    TicketsSkipped = 1
                };
            }
        }

        try
        {
            var ticketResult = await IndexFirstCommentRowAsync(row, options.ProcessImages, cancellationToken);
            if (ticketResult.Chunks == 0)
            {
                return new FirstCommentIndexResult
                {
                    TicketsProcessed = 1,
                    TicketsSkipped = 1
                };
            }

            return new FirstCommentIndexResult
            {
                TicketsProcessed = 1,
                TicketsIndexed = 1,
                ChunksCreated = ticketResult.Chunks,
                EmbeddingsCreated = ticketResult.Embeddings,
                ProcessImages = options.ProcessImages,
                ImagesDetected = ticketResult.ImagesDetected,
                ImagesExtracted = ticketResult.ImagesExtracted,
                ImageExtractionWarning = ticketResult.ImageExtractionWarning,
                UsageEstimate = ticketResult.Usage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexando comentario #1 del ticket {TicketId}.", row.TicketId);
            return new FirstCommentIndexResult
            {
                TicketsProcessed = 1,
                TicketsFailed = 1,
                Errors = [$"Ticket #{row.TicketNumber} (Id={row.TicketId}): {ex.Message}"]
            };
        }
    }

    private static FirstCommentIndexResult SingleTicketError(string message) =>
        new()
        {
            TicketsProcessed = 1,
            TicketsFailed = 1,
            Errors = [message]
        };

    private async Task<FirstCommentRowIndexResult> IndexFirstCommentRowAsync(
        TicketFirstCommentRow row,
        bool processImages,
        CancellationToken cancellationToken)
    {
        var commentContent = await _commentContentService.ToIndexableContentAsync(
            row.Content,
            new CommentContentRequest
            {
                ProcessImages = processImages,
                ImageCacheMode = processImages
                    ? ImageExtractionCacheMode.UseAndRefresh
                    : ImageExtractionCacheMode.CacheOnly,
                TicketId = row.TicketId,
                TicketActionId = row.TicketActionId
            },
            cancellationToken);

        var document = FirstCommentDocumentBuilder.Build(row, commentContent.Text);
        if (string.IsNullOrWhiteSpace(document))
        {
            return new FirstCommentRowIndexResult(
                0,
                0,
                null,
                commentContent.ImagesDetected,
                commentContent.ImagesExtracted,
                commentContent.ImageExtractionWarning);
        }

        var imageCount = processImages
            ? commentContent.ImagesExtracted > 0
                ? commentContent.ImagesExtracted
                : TicketHtmlHelper.ExtractImageSources(row.Content).Count
            : 0;

        var usage = _tokenEstimateService.EstimateTicketIndex(
            document,
            imageCount,
            processImages);

        await _vectorRepository.DeleteChunksByTicketAndSourceAsync(
            row.TicketId,
            DocumentChunkSource.ClientFirstComment,
            cancellationToken);

        var chunks = _chunkingService.SplitIntoChunks(
            document,
            row.TicketId,
            row.TicketNumber,
            DocumentChunkSource.ClientFirstComment,
            row.TicketActionId);

        if (chunks.Count == 0)
        {
            return new FirstCommentRowIndexResult(
                0,
                0,
                null,
                commentContent.ImagesDetected,
                commentContent.ImagesExtracted,
                commentContent.ImageExtractionWarning);
        }

        await _vectorRepository.SaveChunksAsync(chunks, cancellationToken);

        var embeddings = new List<TicketEmbedding>();
        foreach (var chunk in chunks)
        {
            var vector = await _embeddingService.CreateEmbeddingAsync(chunk.Content, cancellationToken);
            embeddings.Add(new TicketEmbedding
            {
                DocumentChunkId = chunk.Id,
                TicketId = row.TicketId,
                Model = _embeddingService.ModelName,
                Embedding = new Vector(vector)
            });
        }

        await _vectorRepository.SaveEmbeddingsAsync(embeddings, cancellationToken);
        return new FirstCommentRowIndexResult(
            chunks.Count,
            embeddings.Count,
            usage,
            commentContent.ImagesDetected,
            commentContent.ImagesExtracted,
            commentContent.ImageExtractionWarning);
    }

    private sealed record FirstCommentRowIndexResult(
        int Chunks,
        int Embeddings,
        TokenUsageEstimate? Usage,
        int ImagesDetected,
        int ImagesExtracted,
        string? ImageExtractionWarning);
}
