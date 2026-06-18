using MoneyPenny.Models.Tickets;
using MoneyPenny.ViewModels.Tickets;
using Microsoft.EntityFrameworkCore;

namespace MoneyPenny.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TicketsDbContext _context;

    public TicketRepository(TicketsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(
        TicketFilters filters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            query = query.Where(t =>
                t.Number.Contains(filters.Search) ||
                t.Title.Contains(filters.Search) ||
                t.Description.Contains(filters.Search));
        }

        var status = !string.IsNullOrWhiteSpace(filters.Status)
            ? filters.Status
            : filters.StatusText;

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filters.Group))
        {
            query = query.Where(t => t.Group == filters.Group);
        }

        if (!string.IsNullOrWhiteSpace(filters.Customer))
        {
            query = query.Where(t => t.Customer == filters.Customer);
        }

        if (!string.IsNullOrWhiteSpace(filters.Product))
        {
            query = query.Where(t => t.Product == filters.Product);
        }

        if (!string.IsNullOrWhiteSpace(filters.Priority))
        {
            query = query.Where(t => t.Priority == filters.Priority);
        }

        IQueryable<Ticket> ordered = query.OrderByDescending(t => t.CreatedAt);

        ordered = filters.ResultLimit switch
        {
            "100" => ordered.Take(100),
            "all" => ordered,
            _ => ordered.Take(50)
        };

        return await ordered.ToListAsync(cancellationToken);
    }

    public async Task<TicketFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        return new TicketFilterOptions
        {
            Groups = await GetDistinctAsync(t => t.Group, cancellationToken),
            Customers = await GetDistinctAsync(t => t.Customer, cancellationToken),
            Products = await GetDistinctAsync(t => t.Product, cancellationToken),
            Statuses = await GetDistinctAsync(t => t.Status, cancellationToken),
            Priorities = await GetDistinctAsync(t => t.Priority, cancellationToken)
        };
    }

    public Task<Ticket?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<Ticket?> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        return _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Number == number, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketAction>> GetActionsByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TicketActions
            .AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetDistinctAsync(
        System.Linq.Expressions.Expression<Func<Ticket, string?>> selector,
        CancellationToken cancellationToken)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Select(selector)
            .Where(value => value != null && value != "")
            .Distinct()
            .OrderBy(value => value)
            .Select(value => value!)
            .ToListAsync(cancellationToken);
    }
}
