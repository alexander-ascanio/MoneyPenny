using MoneyPenny.Models.Rag;
using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Services.Rag.Export;

public interface IRatedTicketsExportService
{
    Task<RatedTicketsExportResultViewModel> GetRatedTicketsAsync(
        int page,
        int pageSize,
        RagResponseType? responseType = RagResponseType.Gpt,
        CancellationToken cancellationToken = default);
}
