namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentIndexOptions
{
    public bool RebuildAll { get; init; }
    public bool SkipAlreadyIndexed { get; init; } = true;
    public bool ProcessImages { get; init; }
    public bool OnlyTicketsListScope { get; init; } = true;
    public int? MaxTickets { get; init; }
}
