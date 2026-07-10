namespace MoneyPenny.ViewModels.Rag;

public class RagAnswerRatingViewModel
{
    public int QueryLogId { get; init; }
    public short? Rating { get; init; }
    public bool ShowNotAnswerableOption { get; init; }
}
