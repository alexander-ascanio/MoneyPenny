namespace MoneyPenny.ViewModels.Rag;

/// <summary>Resultado de una valoración para mostrar como página de confirmación (enlaces GET).</summary>
public class RagRateAnswerResultViewModel
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? TicketNumber { get; init; }
    public int? TicketId { get; init; }
    public int? QueryLogId { get; init; }
    public short? Rating { get; init; }
}
