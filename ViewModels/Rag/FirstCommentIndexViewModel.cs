using MoneyPenny.ViewModels.Shared;

using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.ViewModels.Rag;

public class FirstCommentIndexViewModel
{
    public int TotalTicketsWithFirstComment { get; set; }
    public int IndexedTickets { get; set; }
    public int PendingTickets { get; set; }

    public int KnowledgeBaseTotalTicketsWithFirstComment { get; set; }
    public int KnowledgeBaseIndexedTickets { get; set; }
    public int KnowledgeBasePendingTickets { get; set; }

    public bool RebuildAll { get; set; }
    public bool SkipAlreadyIndexed { get; set; } = true;
    public bool ProcessImages { get; set; } = true;
    public bool OnlyKnowledgeBaseTickets { get; set; }
    public int? MaxTickets { get; set; }

    [Display(Name = "Fecha creación desde")]
    [DataType(DataType.Date)]
    public DateTime? TicketCreatedFrom { get; set; }

    [Display(Name = "Fecha creación hasta")]
    [DataType(DataType.Date)]
    public DateTime? TicketCreatedTo { get; set; }

    [Display(Name = "Número de ticket")]
    public string? TargetTicketNumber { get; set; }

    public bool SkipAlreadyIndexedSingle { get; set; } = true;
    public bool RebuildAllSingle { get; set; }
    public bool ProcessImagesSingle { get; set; } = true;
    public bool OnlyKnowledgeBaseTicketsSingle { get; set; }

    public int AverageCommentCharCount { get; set; }
    public double AverageImagesPerTicket { get; set; }
    public int CorpusSampleSize { get; set; }

    public TokenUsageEstimateViewModel? RunEstimate { get; set; }
    public TokenUsageEstimateViewModel? LastRunEstimate { get; set; }
    public RagTokenPricingConfigViewModel PricingConfig { get; set; } = new();

    public FirstCommentIndexResultViewModel? LastResult { get; set; }
    public string? SuccessMessage { get; set; }
}

public class FirstCommentIndexResultViewModel
{
    public int TicketsProcessed { get; init; }
    public int TicketsIndexed { get; init; }
    public int TicketsSkipped { get; init; }
    public int TicketsFailed { get; init; }
    public int ChunksCreated { get; init; }
    public int EmbeddingsCreated { get; init; }
    public int ImagesDetected { get; init; }
    public int ImagesExtracted { get; init; }
    public bool ProcessImages { get; init; }
    public string? ImageExtractionWarning { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
