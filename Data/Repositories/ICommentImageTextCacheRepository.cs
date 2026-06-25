namespace MoneyPenny.Data.Repositories;

public interface ICommentImageTextCacheRepository
{
    Task<IReadOnlyDictionary<string, string>> GetByImageSourcesAsync(
        IEnumerable<string> normalizedImageSources,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        int ticketId,
        int ticketActionId,
        string normalizedImageSource,
        string extractedText,
        string visionModel,
        CancellationToken cancellationToken = default);
}
