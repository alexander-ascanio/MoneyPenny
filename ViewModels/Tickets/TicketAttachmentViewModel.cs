namespace MoneyPenny.ViewModels.Tickets;

public class TicketAttachmentViewModel
{
    public required string OriginalUrl { get; init; }
    public string? FileName { get; init; }
    public bool IsImage { get; init; }
}
