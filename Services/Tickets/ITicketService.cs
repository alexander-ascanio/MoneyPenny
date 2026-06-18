using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Services.Tickets;

public interface ITicketService
{
    Task<TicketListViewModel> GetListAsync(string? search = null, string? status = null, CancellationToken cancellationToken = default);
    Task<TicketDetailViewModel?> GetDetailAsync(int id, CancellationToken cancellationToken = default);
}
