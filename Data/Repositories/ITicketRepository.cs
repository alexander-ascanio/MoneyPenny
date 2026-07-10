using MoneyPenny.Models.Tickets;
using MoneyPenny.Services.Rag.Export;
using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Data.Repositories;

public interface ITicketRepository
{
    Task<IReadOnlyList<Ticket>> GetAllAsync(TicketFilters filters, CancellationToken cancellationToken = default);
    Task<TicketFilterOptions> GetFilterOptionsAsync(
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketAction>> GetActionsByTicketIdAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<TicketAction?> GetOldestActionWithContentByTicketIdAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<int> CountTicketsWithFirstCommentAsync(
        bool onlyTicketsListScope = true,
        CancellationToken cancellationToken = default);
    Task<int> CountFirstCommentsAsync(
        bool onlyKnowledgeBaseScope = false,
        DateTime? ticketCreatedFrom = null,
        DateTime? ticketCreatedTo = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFirstCommentTicketIdsAsync(
        bool onlyKnowledgeBaseScope = false,
        DateTime? ticketCreatedFrom = null,
        DateTime? ticketCreatedTo = null,
        CancellationToken cancellationToken = default);
    Task<int> CountKnowledgeBaseTicketsWithFirstCommentAsync(
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetKnowledgeBaseIndexCountsTicketIdsWithFirstCommentAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketFirstCommentRow>> GetFirstCommentsPageAsync(
        int skip,
        int take,
        bool onlyKnowledgeBaseScope = false,
        DateTime? ticketCreatedFrom = null,
        DateTime? ticketCreatedTo = null,
        CancellationToken cancellationToken = default);
    Task<FirstCommentCorpusStats> GetFirstCommentCorpusStatsAsync(
        int sampleSize,
        bool onlyKnowledgeBaseScope = false,
        CancellationToken cancellationToken = default);
    Task<TicketFirstCommentRow?> GetFirstCommentByTicketNumberAsync(
        string ticketNumber,
        bool? onlyKnowledgeBaseScope = false,
        CancellationToken cancellationToken = default);
    Task<TicketFirstCommentRow?> GetFirstCommentByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetTicketIdsInNonKnowledgeBaseScopeAsync(
        IEnumerable<int> ticketIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndexedTicketsMonthCount>> GetTicketCountsByCreatedMonthAsync(
        IReadOnlyCollection<int> ticketIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, TicketExportLookup>> GetTicketExportLookupsByIdsAsync(
        IReadOnlyCollection<int> ticketIds,
        CancellationToken cancellationToken = default);
}
