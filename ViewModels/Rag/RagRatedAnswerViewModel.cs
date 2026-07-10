using MoneyPenny.Models.Rag;

namespace MoneyPenny.ViewModels.Rag;

public class RagRatedAnswerViewModel
{
    public int QueryLogId { get; init; }
    public string Answer { get; init; } = string.Empty;
    public short Rating { get; init; }
    public RagResponseType ResponseType { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime RatedAt { get; init; }

    public bool IsPositive => Rating == RagQueryLog.RatingGood;
    public bool IsNotAnswerable => Rating == RagQueryLog.RatingNotAnswerable;
}
