namespace MoneyPenny.ViewModels.Api;

/// <summary>
/// Payload del endpoint público POST /api/tickets: crea un ticket y su acción inicial.
/// </summary>
public class TicketIngestRequest
{
    public TicketIngestTicket? Ticket { get; set; }
    public TicketIngestAction? Action { get; set; }
}

public class TicketIngestTicket
{
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Type { get; set; }
    public string? Source { get; set; }
    public string? CustomerName { get; set; }
    public string? Contacts { get; set; }
    public string? AssignedToName { get; set; }
    public string? TicketNumber { get; set; }
    public string? TeamSupportId { get; set; }
    public string? GroupName { get; set; }
    public string? ProductName { get; set; }
    public string? CodigoTelegestion { get; set; }
    public bool? IsKnowledgeBase { get; set; }
}

public class TicketIngestAction
{
    public string? ActionType { get; set; }
    public string? Content { get; set; }
    public string? CreatedByName { get; set; }
    public bool? IsVisible { get; set; }
    public string? TeamSupportActionId { get; set; }
}

public class TicketIngestResponse
{
    public bool Success { get; set; }
    public int TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public string? TeamSupportId { get; set; }
    public int ActionId { get; set; }
    public string? TeamSupportActionId { get; set; }
}
