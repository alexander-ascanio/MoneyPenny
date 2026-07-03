namespace MoneyPenny.ViewModels.Rag;

public class AskTicketViewModel
{
    public int TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public bool GenerateGptAnswer { get; set; }
    public bool KnowledgeBaseOnly { get; set; }
    public double? MinScoreOverride { get; set; }
    public bool AllowMinScoreFallback { get; set; } = true;
    public bool SkipQueryLog { get; set; }
}
