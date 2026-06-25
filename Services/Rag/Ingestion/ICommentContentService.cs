namespace MoneyPenny.Services.Rag.Ingestion;

public interface ICommentContentService
{
    Task<CommentIndexableContent> ToIndexableContentAsync(
        string? htmlContent,
        bool processImages = true,
        CancellationToken cancellationToken = default);
}
