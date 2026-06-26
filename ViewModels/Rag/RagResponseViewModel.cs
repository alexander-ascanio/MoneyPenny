using MoneyPenny.ViewModels.Shared;

namespace MoneyPenny.ViewModels.Rag;

public class RagContextItemViewModel
{
    public int TicketId { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public double Score { get; init; }
    public string Content { get; init; } = string.Empty;
}

public class RagFirstCommentViewModel
{
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string OriginalContent { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? ImageExtractionWarning { get; init; }
}

public class RagResponseViewModel
{
    public string Answer { get; init; } = string.Empty;
    public bool HasGptAnswer { get; init; }
    public IReadOnlyList<RagContextItemViewModel> ContextItems { get; init; } = [];
    public RagFirstCommentViewModel? FirstComment { get; init; }
    public int TicketId { get; init; }
    public string? TicketNumber { get; init; }
    public string? ErrorMessage { get; init; }
    public TokenUsageEstimateViewModel? GptEstimate { get; set; }
    public TokenUsageEstimateViewModel? LastRunEstimate { get; set; }
}
