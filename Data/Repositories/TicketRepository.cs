using MoneyPenny.Models.Tickets;
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
        string? search = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.Number.Contains(search) ||
                t.Title.Contains(search) ||
                t.Description.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
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
}
