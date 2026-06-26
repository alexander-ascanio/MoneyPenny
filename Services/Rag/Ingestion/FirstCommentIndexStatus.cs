namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentIndexStatus
{
    public int TotalTicketsWithFirstComment { get; init; }
    public int IndexedTickets { get; init; }
    public int PendingTickets => Math.Max(0, TotalTicketsWithFirstComment - IndexedTickets);
    public int AverageCommentCharCount { get; init; }
    public double AverageImagesPerTicket { get; init; }
    public int CorpusSampleSize { get; init; }
}