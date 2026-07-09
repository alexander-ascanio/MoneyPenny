using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Services.Rag.Validation;

public sealed class ResponseGroundingRequest
{
    public required string Answer { get; init; }
    public string? FirstCommentContent { get; init; }
    public IReadOnlyList<RagContextItemViewModel> ContextItems { get; init; } = [];
    public string? TicketNumber { get; init; }
    public string? KnowledgeBaseSolutionText { get; init; }
}

public interface IResponseGroundingChecker
{
    ResponseGroundingReportViewModel Evaluate(ResponseGroundingRequest request);
}
