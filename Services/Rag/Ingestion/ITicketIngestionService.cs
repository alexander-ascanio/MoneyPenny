using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Services.Rag.Ingestion;

public interface ITicketIngestionService
{
    Task<string> BuildTicketDocumentAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task IndexTicketAsync(int ticketId, CancellationToken cancellationToken = default);
}
