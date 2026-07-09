using System.Text;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Rag;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace MoneyPenny.Services.Rag.Ingestion;

public class TicketIngestionService : ITicketIngestionService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IVectorRepository _vectorRepository;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ICommentContentService _commentContentService;
    private readonly IFirstCommentIndexService _firstCommentIndexService;
    private readonly ILogger<TicketIngestionService> _logger;

    public TicketIngestionService(
        ITicketRepository ticketRepository,
        IVectorRepository vectorRepository,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        ICommentContentService commentContentService,
        IFirstCommentIndexService firstCommentIndexService,
        ILogger<TicketIngestionService> logger)
    {
        _ticketRepository = ticketRepository;
        _vectorRepository = vectorRepository;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _commentContentService = commentContentService;
        _firstCommentIndexService = firstCommentIndexService;
        _logger = logger;
    }

    public async Task<string> BuildTicketDocumentAsync(
        Ticket ticket,
        bool processImages = true,
        CancellationToken cancellationToken = default)
    {
        var (document, _) = await BuildTicketDocumentInternalAsync(ticket, processImages, cancellationToken);
        return document;
    }

    public async Task<TicketIndexResult> IndexTicketAsync(
        int ticketId,
        bool processImages = true,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket {ticketId} no encontrado.");

        var (document, commentContent) = await BuildTicketDocumentInternalAsync(
            ticket,
            processImages,
            cancellationToken);

        await _vectorRepository.DeleteTicketIndexAsync(ticketId, cancellationToken);

        var chunks = _chunkingService.SplitIntoChunks(
            document,
            ticket.Id,
            ticket.Number,
            isKnowledgeBase: ticket.IsKnowledgeBase);
        await _vectorRepository.SaveChunksAsync(chunks, cancellationToken);

        var embeddings = new List<TicketEmbedding>();
        foreach (var chunk in chunks)
        {
            var vector = await _embeddingService.CreateEmbeddingAsync(chunk.Content, cancellationToken);
            embeddings.Add(new TicketEmbedding
            {
                DocumentChunkId = chunk.Id,
                TicketId = ticket.Id,
                Model = _embeddingService.ModelName,
                Embedding = new Vector(vector)
            });
        }

        await _vectorRepository.SaveEmbeddingsAsync(embeddings, cancellationToken);

        await SyncFirstCommentIndexAsync(ticket, processImages, cancellationToken);

        _logger.LogInformation(
            "Ticket {TicketId} indexado con {ChunkCount} chunks (processImages={ProcessImages}, images={ImagesExtracted}/{ImagesDetected}).",
            ticketId,
            chunks.Count,
            processImages,
            commentContent.ImagesExtracted,
            commentContent.ImagesDetected);

        return new TicketIndexResult
        {
            ChunkCount = chunks.Count,
            ProcessImages = processImages,
            ImagesDetected = commentContent.ImagesDetected,
            ImagesExtracted = commentContent.ImagesExtracted,
            ImageExtractionWarning = commentContent.ImageExtractionWarning
        };
    }

    private async Task<(string Document, CommentIndexableContent CommentContent)> BuildTicketDocumentInternalAsync(
        Ticket ticket,
        bool processImages,
        CancellationToken cancellationToken)
    {
        var document = new StringBuilder();
        document.AppendLine($"Ticket: {ticket.Number}");
        document.AppendLine($"Título: {ticket.Title}");
        document.AppendLine($"Estado: {ticket.Status}");
        document.AppendLine($"Prioridad: {ticket.Priority}");
        document.AppendLine($"Asignado: {ticket.Assignee ?? "Sin asignar"}");
        document.AppendLine("Descripción:");
        document.AppendLine(TicketHtmlHelper.ToPlainText(ticket.Description));

        var commentContent = new CommentIndexableContent();
        var oldestComment = await _ticketRepository.GetOldestActionWithContentByTicketIdAsync(
            ticket.Id,
            cancellationToken);

        if (oldestComment is not null)
        {
            commentContent = await _commentContentService.ToIndexableContentAsync(
                oldestComment.Content,
                new CommentContentRequest
                {
                    ProcessImages = processImages,
                    ImageCacheMode = ImageExtractionCacheMode.UseAndRefresh,
                    RefreshImageTextCache = processImages,
                    TicketId = ticket.Id,
                    TicketActionId = oldestComment.Id,
                    TeamSupportActionId = oldestComment.TeamSupportActionId,
                    TeamSupportTicketId = ticket.TeamSupportId
                },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(commentContent.Text))
            {
                var author = oldestComment.CreatedByName
                    ?? oldestComment.ModifierName
                    ?? oldestComment.AssignedUsername
                    ?? "Desconocido";

                document.AppendLine();
                document.AppendLine("Primer comentario:");
                document.AppendLine($"Autor: {author}");
                document.AppendLine($"Fecha: {oldestComment.CreatedAt:yyyy-MM-dd HH:mm} UTC");
                document.AppendLine(commentContent.Text);
            }
        }

        return (document.ToString(), commentContent);
    }

    private async Task SyncFirstCommentIndexAsync(
        Ticket ticket,
        bool processImages,
        CancellationToken cancellationToken)
    {
        try
        {
            var syncResult = await _firstCommentIndexService.IndexTicketAsync(
                ticket.Number,
                new FirstCommentIndexOptions
                {
                    ProcessImages = processImages,
                    SkipAlreadyIndexed = false,
                    OnlyKnowledgeBaseTickets = null,
                    RebuildAll = true
                },
                cancellationToken);

            if (syncResult.TicketsFailed > 0)
            {
                _logger.LogWarning(
                    "Ticket {TicketId} indexado en TicketDocument, pero falló la sync del índice #1: {Error}",
                    ticket.Id,
                    syncResult.Errors.FirstOrDefault() ?? "error desconocido");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Ticket {TicketId} indexado en TicketDocument, pero falló la sync del índice #1.",
                ticket.Id);
        }
    }
}
