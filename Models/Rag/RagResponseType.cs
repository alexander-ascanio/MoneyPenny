namespace MoneyPenny.Models.Rag;

public enum RagResponseType
{
    Gpt = 0,
    KnowledgeBase = 1,
    /// <summary>Contexto RAG recuperado (sin respuesta GPT).</summary>
    Context = 2
}
