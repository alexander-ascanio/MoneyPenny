namespace MoneyPenny.ViewModels.Rag;

public class RatedTicketExportItemViewModel
{
    public int QueryLogId { get; init; }
    public int? TicketId { get; init; }
    public string? TicketNumber { get; init; }
    public DateTime? TicketCreatedAt { get; init; }
    public string? TicketStatus { get; init; }
    public short Rating { get; init; }
    public string RatingLabel { get; init; } = string.Empty;
    public DateTime? RatedAt { get; init; }
    public string? TeamSupportLookupError { get; init; }
}
