namespace MoneyPenny.Models.Tickets;

public class TicketFilters
{
    public string? Search { get; set; }
    public string? StatusText { get; set; }
    public string? GroupName { get; set; }
    public string? CustomerName { get; set; }
    public string? Customer { get; set; }
    public string? Product { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    /// <summary>Valores: null/vacío (todos), "true" (indexado RAG), "false" (sin indexar).</summary>
    public string? Rag { get; set; }
    /// <summary>Valores: null/vacío (listado habitual), "true", "false".</summary>
    public string? IsKnowledgeBase { get; set; }
    public string ResultLimit { get; set; } = "50";
    public string? SortBy { get; set; }
    public string SortDir { get; set; } = "desc";

    public bool SortDescending =>
        !string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase);

    public bool? IsKnowledgeBaseFilter => IsKnowledgeBase?.Trim().ToLowerInvariant() switch
    {
        "true" => true,
        "false" => false,
        _ => null
    };

    public bool? RagFilter => Rag?.Trim().ToLowerInvariant() switch
    {
        "true" => true,
        "false" => false,
        _ => null
    };
}
