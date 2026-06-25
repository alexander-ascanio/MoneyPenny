namespace MoneyPenny.Services.Rag.Ingestion;

public sealed class TicketIndexResult
{
    public int ChunkCount { get; init; }
    public bool ProcessImages { get; init; }
    public int ImagesDetected { get; init; }
    public int ImagesExtracted { get; init; }
    public string? ImageExtractionWarning { get; init; }

    public int ImagesFailed => Math.Max(0, ImagesDetected - ImagesExtracted);
}
