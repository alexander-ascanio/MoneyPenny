namespace MoneyPenny.Models.Tickets;

public class TicketFirstCommentRow
{
    public int TicketId { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Product { get; init; }
    public int TicketActionId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime ActionCreatedAt { get; init; }
    public bool IsKnowledgeBase { get; init; }
}
