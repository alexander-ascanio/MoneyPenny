namespace MoneyPenny.ViewModels.Tickets;

public class TicketFilterSelections
{
    public string? GroupName { get; init; }
    public string? CustomerName { get; init; }
    public string? Customer { get; init; }
    public string? Product { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Rag { get; init; }
    public string? HasActions { get; init; }
    public string? IsKnowledgeBase { get; init; }
    public string ResultLimit { get; init; } = "50";
    public string? SortBy { get; init; }
    public string SortDir { get; init; } = "desc";

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(GroupName) ||
        !string.IsNullOrWhiteSpace(CustomerName) ||
        !string.IsNullOrWhiteSpace(Customer) ||
        !string.IsNullOrWhiteSpace(Product) ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Priority) ||
        !string.IsNullOrWhiteSpace(Rag) ||
        !string.IsNullOrWhiteSpace(HasActions) ||
        !string.IsNullOrWhiteSpace(IsKnowledgeBase) ||
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
