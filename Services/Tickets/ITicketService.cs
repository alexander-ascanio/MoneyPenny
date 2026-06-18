using MoneyPenny.Models.Tickets;
using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Services.Tickets;

public interface ITicketService
{
    Task<TicketListViewModel> GetListAsync(TicketFilters filters, CancellationToken cancellationToken = default);
    Task<TicketDetailViewModel?> GetDetailAsync(int id, CancellationToken cancellationToken = default);
}
