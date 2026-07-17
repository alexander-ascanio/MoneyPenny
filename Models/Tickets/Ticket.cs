namespace MoneyPenny.Models.Tickets;

/// <summary>
/// Entidad de lectura mapeada a public.tickets en teamsupport_db (TeamSupport).
/// </summary>
public class Ticket
{
    public int Id { get; set; }
    // Anulable: en TeamSupport existen filas con TicketNumber NULL.
    public string? Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public string? Customer { get; set; }
    public string? Contacts { get; set; }
    public string? TeamSupportId { get; set; }
    public string? CodigoTelegestion { get; set; }
    public string? Group { get; set; }
    public string? Product { get; set; }
    public bool IsKnowledgeBase { get; set; }
    public string? Assignee { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
