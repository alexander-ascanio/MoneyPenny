namespace MoneyPenny.Services.Rag.Ingestion;

public sealed class CommentIndexableContent
{
    public string Text { get; init; } = string.Empty;
    public int ImagesDetected { get; init; }
    public int ImagesExtracted { get; init; }
    public string? ImageExtractionWarning { get; init; }

    public int ImagesFailed => Math.Max(0, ImagesDetected - ImagesExtracted);
}
