namespace MoneyPenny.Models.Tickets;

public class TicketFilters
{
    public string? Search { get; set; }
    public string? StatusText { get; set; }
    public string? Group { get; set; }
    public string? Customer { get; set; }
    public string? Product { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string ResultLimit { get; set; } = "50";
}
