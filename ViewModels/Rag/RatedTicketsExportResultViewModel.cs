namespace MoneyPenny.ViewModels.Rag;

public class RatedTicketsExportResultViewModel
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public string? ResponseType { get; init; }
    public IReadOnlyList<RatedTicketExportItemViewModel> Items { get; init; } = [];
}
