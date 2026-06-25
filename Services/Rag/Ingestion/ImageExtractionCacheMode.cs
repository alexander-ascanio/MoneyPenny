namespace MoneyPenny.Services.Rag.Ingestion;

public enum ImageExtractionCacheMode
{
    /// <summary>Solo usa texto ya cacheado; no llama a Vision.</summary>
    CacheOnly,

    /// <summary>Usa caché y llama a Vision solo para imágenes no cacheadas.</summary>
    UseAndRefresh
}
