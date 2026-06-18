namespace MoneyPenny.Models.Tickets;

public class TicketAction
{
    public int Id { get; set; }
    public string? TeamSupportActionId { get; set; }
    public int TicketId { get; set; }
    public string? ActionType { get; set; }
    public string? Content { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? TimeSpentMinutes { get; set; }
    public string? TicketStatus { get; set; }
    public string? Source { get; set; }
    public string? ModifierName { get; set; }
    public bool IsVisible { get; set; }
    public string? AssignedUsername { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
