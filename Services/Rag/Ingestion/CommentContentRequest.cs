namespace MoneyPenny.Services.Rag.Ingestion;

public class CommentContentRequest
{
    public bool ProcessImages { get; init; } = true;
    public ImageExtractionCacheMode ImageCacheMode { get; init; } = ImageExtractionCacheMode.UseAndRefresh;
    /// <summary>Fuerza nueva extracción Vision aunque exista texto en caché (p. ej. al reindexar desde Details).</summary>
    public bool RefreshImageTextCache { get; init; }
    public int? TicketId { get; init; }
    public int? TicketActionId { get; init; }
    public string? TeamSupportActionId { get; init; }
    public string? TeamSupportTicketId { get; init; }
}
