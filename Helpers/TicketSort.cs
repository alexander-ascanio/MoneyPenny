using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Helpers;

public static class TicketSort
{
    public const string Number = "number";
    public const string Subject = "subject";
    public const string Customer = "customer";
    public const string Status = "status";
    public const string Priority = "priority";
    public const string Created = "created";
    public const string Modified = "modified";
    public const string Indexed = "indexed";
    public const string Rag = "rag";

    public static bool IsInMemorySort(string? sortBy) =>
        string.Equals(sortBy, Indexed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sortBy, Rag, StringComparison.OrdinalIgnoreCase);

    public static IQueryable<Ticket> Apply(
        IQueryable<Ticket> query,
        string? sortBy,
        bool descending)
    {
        if (IsInMemorySort(sortBy))
        {
            return query.OrderByDescending(t => t.CreatedAt);
        }

        return (sortBy ?? Created).Trim().ToLowerInvariant() switch
        {
            Number => descending
                ? query.OrderByDescending(t => t.Number)
                : query.OrderBy(t => t.Number),
            Subject => descending
                ? query.OrderByDescending(t => t.Title)
                : query.OrderBy(t => t.Title),
            Customer => descending
                ? query.OrderByDescending(t => t.Customer)
                : query.OrderBy(t => t.Customer),
            Status => descending
                ? query.OrderByDescending(t => t.Status)
                : query.OrderBy(t => t.Status),
            Priority => descending
                ? query.OrderByDescending(t => t.Priority)
                : query.OrderBy(t => t.Priority),
            Modified => descending
                ? query.OrderByDescending(t => t.UpdatedAt)
                : query.OrderBy(t => t.UpdatedAt),
            Created => descending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),
            _ => descending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt)
        };
    }
}
