namespace MoneyPenny.ViewModels.Rag;

public class RagContextItemViewModel
{
    public int ChunkIndex { get; init; }
    public double Score { get; init; }
    public string Content { get; init; } = string.Empty;
}

public class RagFirstCommentViewModel
{
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ImageExtractionWarning { get; init; }
}

public class RagResponseViewModel
{
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public IReadOnlyList<RagContextItemViewModel> ContextItems { get; init; } = [];
    public RagFirstCommentViewModel? FirstComment { get; init; }
    public int? TicketId { get; init; }
    public string? TicketNumber { get; init; }
    public bool PreviewContextOnly { get; init; }
}
