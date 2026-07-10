namespace MoneyPenny.Services.Rag.Export;

public sealed class TicketExportLookup
{
    public string? TeamSupportId { get; init; }
    public string Number { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
}
