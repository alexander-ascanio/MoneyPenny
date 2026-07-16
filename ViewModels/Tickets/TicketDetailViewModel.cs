namespace MoneyPenny.ViewModels.Tickets;

public class TicketDetailViewModel
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Customer { get; init; }
    public string? Contacts { get; init; }
    public string? TeamSupportId { get; init; }
    public string? CodigoTelegestion { get; init; }
    public string? Assignee { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsIndexed { get; init; }
    public bool IsFirstCommentIndexed { get; init; }
    public bool HasGeneratedContext { get; init; }
    public int FirstCommentImageCount { get; init; }
    public bool PromptImageProcessingOnIndex { get; init; }
    public ViewModels.Shared.TokenUsageEstimateViewModel? IndexWithoutImagesEstimate { get; init; }
    public ViewModels.Shared.TokenUsageEstimateViewModel? IndexWithImagesEstimate { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<TicketActionViewModel> Comments { get; init; } = [];
}
