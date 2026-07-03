using Microsoft.Extensions.Caching.Memory;

namespace MoneyPenny.Services.Rag;

public class RagAskCachedResult
{
    public required ViewModels.Rag.RagResponseViewModel Response { get; init; }
}

public interface IRagAskResultCache
{
    string Store(string userId, RagAskCachedResult result);
    RagAskCachedResult? Get(string userId, string key);
}

public class RagAskResultCache : IRagAskResultCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(20);
    private readonly IMemoryCache _cache;

    public RagAskResultCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Store(string userId, RagAskCachedResult result)
    {
        var key = Guid.NewGuid().ToString("N");
        _cache.Set(BuildCacheKey(userId, key), result, CacheExpiration);
        return key;
    }

    public RagAskCachedResult? Get(string userId, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return _cache.TryGetValue(BuildCacheKey(userId, key), out RagAskCachedResult? result)
            ? result
            : null;
    }

    private static string BuildCacheKey(string userId, string key) => $"rag-ask:{userId}:{key}";
}
