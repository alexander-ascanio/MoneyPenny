namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentIndexOptions
{
    public bool RebuildAll { get; init; }
    public bool SkipAlreadyIndexed { get; init; } = true;
    public bool ProcessImages { get; init; }
    public bool? OnlyKnowledgeBaseTickets { get; init; }
    public int? MaxTickets { get; init; }
    public DateTime? TicketCreatedFrom { get; init; }
    public DateTime? TicketCreatedTo { get; init; }
}
