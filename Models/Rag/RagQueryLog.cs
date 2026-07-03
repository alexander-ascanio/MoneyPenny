namespace MoneyPenny.Models.Rag;

public class RagQueryLog
{
    public const short RatingGood = 1;
    public const short RatingBad = -1;
    /// <summary>Valor enviado desde la UI para quitar la valoración (se persiste como null).</summary>
    public const short RatingClear = 0;

    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? TicketId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public RagResponseType ResponseType { get; set; } = RagResponseType.Gpt;
    public short? Rating { get; set; }
    public string? RatedByUserId { get; set; }
    public DateTime? RatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
