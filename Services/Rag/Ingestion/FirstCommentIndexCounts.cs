namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentIndexCounts
{
    public int TotalTicketsWithFirstComment { get; init; }
    public int IndexedTickets { get; init; }
    public int PendingTickets => Math.Max(0, TotalTicketsWithFirstComment - IndexedTickets);

    public int KnowledgeBaseTotalTicketsWithFirstComment { get; init; }
    public int KnowledgeBaseIndexedTickets { get; init; }
    public int KnowledgeBasePendingTickets =>
        Math.Max(0, KnowledgeBaseTotalTicketsWithFirstComment - KnowledgeBaseIndexedTickets);
}
