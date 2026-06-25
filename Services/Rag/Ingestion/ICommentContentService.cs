namespace MoneyPenny.Services.Rag.Ingestion;

public interface ICommentContentService
{
    Task<CommentIndexableContent> ToIndexableContentAsync(
        string? htmlContent,
        CommentContentRequest? request = null,
        CancellationToken cancellationToken = default);
}
