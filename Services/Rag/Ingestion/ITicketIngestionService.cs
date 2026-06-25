using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Services.Rag.Ingestion;

public interface ITicketIngestionService
{
    Task<string> BuildTicketDocumentAsync(
        Ticket ticket,
        bool processImages = true,
        CancellationToken cancellationToken = default);

    Task<TicketIndexResult> IndexTicketAsync(
        int ticketId,
        bool processImages = true,
        CancellationToken cancellationToken = default);
}
