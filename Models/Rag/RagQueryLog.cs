namespace MoneyPenny.Models.Rag;

public class RagQueryLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? TicketId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
