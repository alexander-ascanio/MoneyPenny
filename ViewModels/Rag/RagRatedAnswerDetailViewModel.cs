namespace MoneyPenny.ViewModels.Rag;

/// <summary>Detalle de una respuesta valorada para el modal de estadísticas (réplica de la vista Ask).</summary>
public class RagRatedAnswerDetailViewModel
{
    public required RagResponseViewModel Response { get; init; }
    public string Question { get; init; } = string.Empty;
    /// <summary>Contexto persistido con la respuesta (null en registros antiguos).</summary>
    public string? StoredContext { get; init; }
    public short? Rating { get; init; }
    public DateTime? RatedAt { get; init; }
}
