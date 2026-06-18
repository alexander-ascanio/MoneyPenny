namespace MoneyPenny.ViewModels.Tickets;

public class TicketActionViewModel
{
    public int Id { get; init; }
    public string? ActionType { get; init; }
    public string? Content { get; init; }
    public string Author { get; init; } = "Sistema";
    public DateTime CreatedAt { get; init; }
    public string? TicketStatus { get; init; }
    public string? Source { get; init; }
    public decimal? TimeSpentMinutes { get; init; }
    public bool IsVisible { get; init; }
}
