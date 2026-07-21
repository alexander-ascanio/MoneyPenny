namespace MoneyPenny.Models.Rag;

/// <summary>Fila agregada de valoraciones por día (fecha de valoración) y tipo de valoración.</summary>
public class RagRatingDailyStatsRow
{
    public DateTime Date { get; set; }
    public short Rating { get; set; }
    public int Count { get; set; }
}
