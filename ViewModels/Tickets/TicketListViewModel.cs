namespace MoneyPenny.ViewModels.Tickets;

public class TicketListViewModel
{
    public IReadOnlyList<TicketListItemViewModel> Tickets { get; init; } = [];
    public string? Search { get; init; }
    public string? StatusFilter { get; init; }
    public string? ErrorMessage { get; init; }
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
    public DateTime CreatedAt { get; init; }
    public bool IsIndexed { get; init; }
}
