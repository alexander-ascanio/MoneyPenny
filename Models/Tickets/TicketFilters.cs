namespace MoneyPenny.Models.Tickets;

public class TicketFilters
{
    public string? Search { get; set; }
    public string? StatusText { get; set; }
    public string? GroupName { get; set; }
    public string? Customer { get; set; }
    public string? Product { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    /// <summary>Valores: null/vacío (listado habitual), "true", "false".</summary>
    public string? IsKnowledgeBase { get; set; }
    public string ResultLimit { get; set; } = "50";

    public bool? IsKnowledgeBaseFilter => IsKnowledgeBase?.Trim().ToLowerInvariant() switch
    {
        "true" => true,
        "false" => false,
        _ => null
    };
}
