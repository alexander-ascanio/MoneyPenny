namespace MoneyPenny.ViewModels.Rag;

public class RagThresholdComparisonColumnViewModel
{
    public double MinScore { get; init; }
    public IReadOnlyList<RagContextItemViewModel> ContextItems { get; init; } = [];
    public string Answer { get; init; } = string.Empty;
    public bool HasGptAnswer { get; init; }
    public string? ErrorMessage { get; init; }
}

public class RagThresholdComparisonViewModel
{
    public int TicketId { get; init; }
    public string? TicketNumber { get; init; }
    public bool KnowledgeBaseOnly { get; init; }
    public RagFirstCommentViewModel? FirstComment { get; init; }
    public string? ErrorMessage { get; init; }
    public bool HasComparison { get; init; }
    public IReadOnlyList<double> ThresholdValues { get; init; } = [];
    public IReadOnlyList<RagThresholdComparisonColumnViewModel> Columns { get; init; } = [];
}
