using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Data.Repositories;

public interface ITicketRepository
{
    Task<IReadOnlyList<Ticket>> GetAllAsync(string? search = null, string? status = null, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
}
