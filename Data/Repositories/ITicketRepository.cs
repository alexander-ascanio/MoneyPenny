using MoneyPenny.Models.Tickets;
using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Data.Repositories;

public interface ITicketRepository
{
    Task<IReadOnlyList<Ticket>> GetAllAsync(TicketFilters filters, CancellationToken cancellationToken = default);
    Task<TicketFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketAction>> GetActionsByTicketIdAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<TicketAction?> GetOldestActionWithContentByTicketIdAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<int> CountTicketsWithFirstCommentAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketFirstCommentRow>> GetFirstCommentsPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task<FirstCommentCorpusStats> GetFirstCommentCorpusStatsAsync(
        int sampleSize,
        CancellationToken cancellationToken = default);
    Task<TicketFirstCommentRow?> GetFirstCommentByTicketNumberAsync(
        string ticketNumber,
        CancellationToken cancellationToken = default);
    Task<TicketFirstCommentRow?> GetFirstCommentByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default);
}
