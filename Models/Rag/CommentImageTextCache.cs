namespace MoneyPenny.Models.Rag;

public class CommentImageTextCache
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int TicketActionId { get; set; }
    public string ImageSource { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public string VisionModel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
