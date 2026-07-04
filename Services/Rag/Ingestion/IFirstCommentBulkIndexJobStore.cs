namespace MoneyPenny.Services.Rag.Ingestion;

public interface IFirstCommentBulkIndexJobStore
{
    string StartJob(string userId, FirstCommentIndexOptions options);
    FirstCommentBulkIndexJobProgress? GetJob(string userId, string jobId);
    bool HasActiveJob(string userId);
    void UpdateProgress(string userId, string jobId, FirstCommentBulkIndexProgressSnapshot snapshot);
    void CompleteJob(string userId, string jobId, FirstCommentIndexResult result);
    void FailJob(string userId, string jobId, string errorMessage);
}
