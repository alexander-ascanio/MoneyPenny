namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentIndexResult
{
    public int TicketsProcessed { get; init; }
    public int TicketsIndexed { get; init; }
    public int TicketsSkipped { get; init; }
    public int TicketsFailed { get; init; }
    public int ChunksCreated { get; init; }
    public int EmbeddingsCreated { get; init; }
    public int ImagesDetected { get; init; }
    public int ImagesExtracted { get; init; }
    public bool ProcessImages { get; init; }
    public string? ImageExtractionWarning { get; init; }
    public Pricing.TokenUsageEstimate? UsageEstimate { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
