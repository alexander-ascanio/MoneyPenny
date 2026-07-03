namespace MoneyPenny.ViewModels.Tickets;

public class TicketListViewModel
{
    public IReadOnlyList<TicketListItemViewModel> Tickets { get; init; } = [];
    public string? Search { get; init; }
    public string? StatusFilter { get; init; }
    public TicketFilterSelections Filters { get; init; } = new();
    public TicketFilterOptions FilterOptions { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public string? SortBy { get; init; }
    public string SortDir { get; init; } = "desc";
    public int TotalCount => Tickets.Count;
}

public class TicketListItemViewModel
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Customer { get; init; }
    public string? Contacts { get; init; }
    public string? TeamSupportId { get; init; }
    public string? CodigoTelegestion { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsFirstCommentIndexed { get; init; }
    public bool IsIndexed { get; init; }
}
