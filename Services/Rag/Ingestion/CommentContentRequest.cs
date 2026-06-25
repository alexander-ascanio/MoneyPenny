namespace MoneyPenny.Services.Rag.Ingestion;

public class CommentContentRequest
{
    public bool ProcessImages { get; init; } = true;
    public ImageExtractionCacheMode ImageCacheMode { get; init; } = ImageExtractionCacheMode.UseAndRefresh;
    public int? TicketId { get; init; }
    public int? TicketActionId { get; init; }
}
