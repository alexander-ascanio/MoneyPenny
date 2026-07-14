using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Services.Rag;

public interface ITicketRagProcessService
{
    Task<TicketRagProcessResultViewModel> ProcessTicketAsync(
        int? ticketId,
        string? ticketNumber,
        string userId,
        bool processImages = true,
        CancellationToken cancellationToken = default);
}
