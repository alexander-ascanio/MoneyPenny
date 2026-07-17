namespace MoneyPenny.Models.Rag;

/// <summary>Fila agregada de valoraciones por mes y tipo de valoración.</summary>
public class RagRatingMonthlyStatsRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public short Rating { get; set; }
    public int Count { get; set; }
}
