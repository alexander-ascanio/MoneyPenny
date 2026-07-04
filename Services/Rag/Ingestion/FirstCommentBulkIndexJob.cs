namespace MoneyPenny.Services.Rag.Ingestion;

public enum FirstCommentBulkIndexJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public sealed record FirstCommentBulkIndexJobProgress
{
    public FirstCommentBulkIndexJobStatus Status { get; init; } = FirstCommentBulkIndexJobStatus.Queued;
    public string Phase { get; init; } = "Preparando";
    public int TotalTickets { get; init; }
    public int Processed { get; init; }
    public int Indexed { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int ChunksCreated { get; init; }
    public string? CurrentTicketNumber { get; init; }
    public int PercentComplete { get; init; }
    public string? ErrorMessage { get; init; }
    public FirstCommentIndexResult? Result { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }

    public static int CalculatePercent(int processed, int totalTickets) =>
        totalTickets > 0
            ? Math.Min(100, (int)Math.Round(processed * 100.0 / totalTickets))
            : 0;
}

public sealed class FirstCommentBulkIndexProgressSnapshot
{
    public string Phase { get; init; } = "Preparando";
    public int TotalTickets { get; init; }
    public int Processed { get; init; }
    public int Indexed { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int ChunksCreated { get; init; }
    public string? CurrentTicketNumber { get; init; }
}

public interface IFirstCommentBulkIndexProgressReporter
{
    void Report(FirstCommentBulkIndexProgressSnapshot snapshot);
}
