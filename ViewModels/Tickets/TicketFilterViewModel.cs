namespace MoneyPenny.ViewModels.Tickets;

public class TicketFilterSelections
{
    public string? Group { get; init; }
    public string? Customer { get; init; }
    public string? Product { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string ResultLimit { get; init; } = "50";

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Group) ||
        !string.IsNullOrWhiteSpace(Customer) ||
        !string.IsNullOrWhiteSpace(Product) ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Priority) ||
        !string.Equals(ResultLimit, "50", StringComparison.Ordinal);
}

public class TicketFilterOptions
{
    public IReadOnlyList<string> Groups { get; init; } = [];
    public IReadOnlyList<string> Customers { get; init; } = [];
    public IReadOnlyList<string> Products { get; init; } = [];
    public IReadOnlyList<string> Statuses { get; init; } = [];
    public IReadOnlyList<string> Priorities { get; init; } = [];
}
