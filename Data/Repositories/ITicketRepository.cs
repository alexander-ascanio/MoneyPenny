using MoneyPenny.Models.Tickets;
using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Data.Repositories;

public interface ITicketRepository
{
    Task<IReadOnlyList<Ticket>> GetAllAsync(TicketFilters filters, CancellationToken cancellationToken = default);
    Task<TicketFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
}
