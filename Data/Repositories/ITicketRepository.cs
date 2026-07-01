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
    Task<int> CountTicketsWithFirstCommentAsync(
        bool onlyTicketsListScope = true,
        CancellationToken cancellationToken = default);
    Task<int> CountKnowledgeBaseTicketsWithFirstCommentAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketFirstCommentRow>> GetFirstCommentsPageAsync(
        int skip,
        int take,
        bool onlyTicketsListScope = true,
        DateTime? ticketCreatedFrom = null,
        DateTime? ticketCreatedTo = null,
        CancellationToken cancellationToken = default);
    Task<FirstCommentCorpusStats> GetFirstCommentCorpusStatsAsync(
        int sampleSize,
        bool onlyTicketsListScope = true,
        CancellationToken cancellationToken = default);
    Task<TicketFirstCommentRow?> GetFirstCommentByTicketNumberAsync(
        string ticketNumber,
        bool onlyTicketsListScope = true,
        CancellationToken cancellationToken = default);
    Task<TicketFirstCommentRow?> GetFirstCommentByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default);
}
