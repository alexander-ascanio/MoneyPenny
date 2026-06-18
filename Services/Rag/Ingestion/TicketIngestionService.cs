using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Models.Rag;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MoneyPenny.Services.Rag.Ingestion;

public class TicketIngestionService : ITicketIngestionService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IVectorRepository _vectorRepository;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<TicketIngestionService> _logger;

    public TicketIngestionService(
        ITicketRepository ticketRepository,
        IVectorRepository vectorRepository,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        ILogger<TicketIngestionService> logger)
    {
        _ticketRepository = ticketRepository;
        _vectorRepository = vectorRepository;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<string> BuildTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var document = new StringBuilder();
        document.AppendLine($"Ticket: {ticket.Number}");
        document.AppendLine($"Título: {ticket.Title}");
        document.AppendLine($"Estado: {ticket.Status}");
        document.AppendLine($"Prioridad: {ticket.Priority}");
        document.AppendLine($"Asignado: {ticket.Assignee ?? "Sin asignar"}");
        document.AppendLine("Descripción:");
        document.AppendLine(ticket.Description);

        var oldestComment = await _ticketRepository.GetOldestActionWithContentByTicketIdAsync(
            ticket.Id,
            cancellationToken);

        if (oldestComment is not null)
        {
            var plainComment = TicketHtmlHelper.ToPlainText(oldestComment.Content);
            if (!string.IsNullOrWhiteSpace(plainComment))
            {
                var author = oldestComment.CreatedByName
                    ?? oldestComment.ModifierName
                    ?? oldestComment.AssignedUsername
                    ?? "Desconocido";

                document.AppendLine();
                document.AppendLine("Primer comentario:");
                document.AppendLine($"Autor: {author}");
                document.AppendLine($"Fecha: {oldestComment.CreatedAt:yyyy-MM-dd HH:mm} UTC");
                document.AppendLine(plainComment);
            }
        }

        return document.ToString();
    }

    public async Task IndexTicketAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket {ticketId} no encontrado.");

        var document = await BuildTicketDocumentAsync(ticket, cancellationToken);
        var chunks = _chunkingService.SplitIntoChunks(document, ticket.Id, ticket.Number);
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
                Vector = vector
            });
        }

        await _vectorRepository.SaveEmbeddingsAsync(embeddings, cancellationToken);
        _logger.LogInformation("Ticket {TicketId} indexado con {ChunkCount} chunks.", ticketId, chunks.Count);
    }
}
