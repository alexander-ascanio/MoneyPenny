namespace MoneyPenny.ViewModels.Rag;

public class RagResponseViewModel
{
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public IReadOnlyList<string> ContextSnippets { get; init; } = [];
    public int? TicketId { get; init; }
    public string? TicketNumber { get; init; }
}
