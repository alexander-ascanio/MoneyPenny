namespace MoneyPenny.ViewModels.Tickets;

public class TicketDetailViewModel
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Assignee { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsIndexed { get; init; }
    public string? ErrorMessage { get; init; }
}
