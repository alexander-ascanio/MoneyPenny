namespace MoneyPenny.Models.Rag;

public enum DocumentChunkSource
{
    /// <summary>Indexación manual por ticket (metadatos + descripción + comentario #1).</summary>
    TicketDocument = 0,

    /// <summary>Comentario #1 del cliente, indexado masivamente para similitud entre tickets.</summary>
    ClientFirstComment = 1
}
