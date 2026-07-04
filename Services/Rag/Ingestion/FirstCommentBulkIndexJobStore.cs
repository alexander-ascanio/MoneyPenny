using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace MoneyPenny.Services.Rag.Ingestion;

public class FirstCommentBulkIndexJobStore : IFirstCommentBulkIndexJobStore
{
    private static readonly TimeSpan JobExpiration = TimeSpan.FromHours(2);
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FirstCommentBulkIndexJobStore> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationSources = new();

    public FirstCommentBulkIndexJobStore(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        ILogger<FirstCommentBulkIndexJobStore> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool HasActiveJob(string userId)
    {
        var activeJobId = GetActiveJobId(userId);
        if (activeJobId is null)
        {
            return false;
        }

        var job = GetJob(userId, activeJobId);
        return job is { Status: FirstCommentBulkIndexJobStatus.Queued or FirstCommentBulkIndexJobStatus.Running };
    }

    public string StartJob(string userId, FirstCommentIndexOptions options)
    {
        if (HasActiveJob(userId))
        {
            throw new InvalidOperationException("Ya hay una indexación masiva en curso.");
        }

        var jobId = Guid.NewGuid().ToString("N");
        var cacheKey = BuildJobKey(userId, jobId);
        var cts = new CancellationTokenSource();

        _cancellationSources[jobId] = cts;
        SetActiveJobId(userId, jobId);

        var initialProgress = new FirstCommentBulkIndexJobProgress
        {
            Status = FirstCommentBulkIndexJobStatus.Queued,
            Phase = "Preparando",
            StartedAt = DateTime.UtcNow
        };
        _cache.Set(cacheKey, initialProgress, JobExpiration);

        _ = RunJobAsync(userId, jobId, options, cts.Token);

        return jobId;
    }

    public FirstCommentBulkIndexJobProgress? GetJob(string userId, string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        return _cache.TryGetValue(BuildJobKey(userId, jobId), out FirstCommentBulkIndexJobProgress? progress)
            ? progress
            : null;
    }

    public void UpdateProgress(string userId, string jobId, FirstCommentBulkIndexProgressSnapshot snapshot)
    {
        var cacheKey = BuildJobKey(userId, jobId);
        if (!_cache.TryGetValue(cacheKey, out FirstCommentBulkIndexJobProgress? current) || current is null)
        {
            return;
        }

        var updated = current with
        {
            Status = FirstCommentBulkIndexJobStatus.Running,
            Phase = snapshot.Phase,
            TotalTickets = snapshot.TotalTickets,
            Processed = snapshot.Processed,
            Indexed = snapshot.Indexed,
            Skipped = snapshot.Skipped,
            Failed = snapshot.Failed,
            ChunksCreated = snapshot.ChunksCreated,
            CurrentTicketNumber = snapshot.CurrentTicketNumber,
            PercentComplete = FirstCommentBulkIndexJobProgress.CalculatePercent(
                snapshot.Processed,
                snapshot.TotalTickets)
        };

        _cache.Set(cacheKey, updated, JobExpiration);
    }

    public void CompleteJob(string userId, string jobId, FirstCommentIndexResult result)
    {
        var cacheKey = BuildJobKey(userId, jobId);
        var completed = new FirstCommentBulkIndexJobProgress
        {
            Status = FirstCommentBulkIndexJobStatus.Completed,
            Phase = "Completado",
            TotalTickets = result.TicketsProcessed,
            Processed = result.TicketsProcessed,
            Indexed = result.TicketsIndexed,
            Skipped = result.TicketsSkipped,
            Failed = result.TicketsFailed,
            ChunksCreated = result.ChunksCreated,
            PercentComplete = 100,
            Result = result,
            StartedAt = _cache.TryGetValue(cacheKey, out FirstCommentBulkIndexJobProgress? current) && current is not null
                ? current.StartedAt
                : DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, completed, JobExpiration);
        ClearActiveJob(userId, jobId);
        CleanupCancellation(jobId);
    }

    public void FailJob(string userId, string jobId, string errorMessage)
    {
        var cacheKey = BuildJobKey(userId, jobId);
        var failed = new FirstCommentBulkIndexJobProgress
        {
            Status = FirstCommentBulkIndexJobStatus.Failed,
            Phase = "Error",
            ErrorMessage = errorMessage,
            StartedAt = _cache.TryGetValue(cacheKey, out FirstCommentBulkIndexJobProgress? current) && current is not null
                ? current.StartedAt
                : DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, failed, JobExpiration);
        ClearActiveJob(userId, jobId);
        CleanupCancellation(jobId);
    }

    private async Task RunJobAsync(
        string userId,
        string jobId,
        FirstCommentIndexOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var indexService = scope.ServiceProvider.GetRequiredService<IFirstCommentIndexService>();
            var reporter = new JobProgressReporter(this, userId, jobId);
            var result = await indexService.IndexAllAsync(options, reporter, cancellationToken);
            CompleteJob(userId, jobId, result);
        }
        catch (OperationCanceledException)
        {
            FailJob(userId, jobId, "La indexación fue cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en indexación masiva (job {JobId}).", jobId);
            FailJob(userId, jobId, ex.Message);
        }
    }

    private void CleanupCancellation(string jobId)
    {
        if (_cancellationSources.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }

    private static string BuildJobKey(string userId, string jobId) => $"first-comment-bulk:{userId}:{jobId}";

    private static string BuildActiveJobKey(string userId) => $"first-comment-bulk-active:{userId}";

    private void SetActiveJobId(string userId, string jobId) =>
        _cache.Set(BuildActiveJobKey(userId), jobId, JobExpiration);

    private string? GetActiveJobId(string userId) =>
        _cache.TryGetValue(BuildActiveJobKey(userId), out string? jobId) ? jobId : null;

    private void ClearActiveJob(string userId, string jobId)
    {
        if (GetActiveJobId(userId) == jobId)
        {
            _cache.Remove(BuildActiveJobKey(userId));
        }
    }

    private sealed class JobProgressReporter(
        IFirstCommentBulkIndexJobStore store,
        string userId,
        string jobId) : IFirstCommentBulkIndexProgressReporter
    {
        public void Report(FirstCommentBulkIndexProgressSnapshot snapshot) =>
            store.UpdateProgress(userId, jobId, snapshot);
    }
}
