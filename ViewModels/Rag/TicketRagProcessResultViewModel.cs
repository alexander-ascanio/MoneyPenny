namespace MoneyPenny.ViewModels.Rag;

public class TicketRagProcessResultViewModel
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TicketRagProcessTicketViewModel? Ticket { get; init; }
    public TicketRagProcessIndexingViewModel? Indexing { get; init; }
    public TicketRagProcessGptViewModel? Gpt { get; init; }
    public ResponseGroundingReportViewModel? GroundingCheck { get; init; }
}

public class TicketRagProcessTicketViewModel
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? TeamSupportId { get; init; }
    public string? Customer { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class TicketRagProcessIndexingViewModel
{
    public int ChunkCount { get; init; }
    public bool ProcessImages { get; init; }
    public int ImagesDetected { get; init; }
    public int ImagesExtracted { get; init; }
    public string? ImageExtractionWarning { get; init; }
}

public class TicketRagProcessGptViewModel
{
    public bool HasAnswer { get; init; }
    public string Answer { get; init; } = string.Empty;
    public int? QueryLogId { get; init; }
    public int ContextTicketCount { get; init; }
}
