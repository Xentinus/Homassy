using System.Collections.Concurrent;
using Homassy.API.Models;

namespace Homassy.API.Services;

public interface IProgressTrackerService
{
    Guid CreateJob();
    void UpdateProgress(Guid jobId, int percentage, ProgressStage stage, ProgressStatus status = ProgressStatus.InProgress);
    void CompleteJob(Guid jobId);
    void FailJob(Guid jobId, string errorMessage);
    void CancelJob(Guid jobId);
    ProgressInfo? GetProgress(Guid jobId);
    CancellationToken GetCancellationToken(Guid jobId);
}

public class ProgressTrackerService : IProgressTrackerService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ProgressInfo> _progressCache = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _jobTtl = TimeSpan.FromMinutes(5);

    public ProgressTrackerService()
    {
        // Cleanup timer runs every minute
        _cleanupTimer = new Timer(CleanupExpiredJobs, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Guid CreateJob()
    {
        var jobId = Guid.NewGuid();
        var progressInfo = new ProgressInfo
        {
            JobId = jobId,
            Percentage = 0,
            Stage = ProgressStage.Validating,
            Status = ProgressStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var cts = new CancellationTokenSource();

        _progressCache.TryAdd(jobId, progressInfo);
        _cancellationTokens.TryAdd(jobId, cts);

        return jobId;
    }

    public void UpdateProgress(Guid jobId, int percentage, ProgressStage stage, ProgressStatus status = ProgressStatus.InProgress)
    {
        if (_progressCache.TryGetValue(jobId, out var progressInfo))
        {
            progressInfo.Percentage = Math.Clamp(percentage, 0, 100);
            progressInfo.Stage = stage;
            progressInfo.Status = status;
            progressInfo.UpdatedAt = DateTime.UtcNow;
        }
    }

    public void CompleteJob(Guid jobId)
    {
        UpdateProgress(jobId, 100, ProgressStage.Completed, ProgressStatus.Completed);
    }

    public void FailJob(Guid jobId, string errorMessage)
    {
        if (_progressCache.TryGetValue(jobId, out var progressInfo))
        {
            progressInfo.Status = ProgressStatus.Failed;
            progressInfo.ErrorMessage = errorMessage;
            progressInfo.UpdatedAt = DateTime.UtcNow;
        }
    }

    public void CancelJob(Guid jobId)
    {
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
        }

        UpdateProgress(jobId, 0, ProgressStage.Validating, ProgressStatus.Cancelled);
    }

    public ProgressInfo? GetProgress(Guid jobId)
    {
        _progressCache.TryGetValue(jobId, out var progressInfo);
        return progressInfo;
    }

    public CancellationToken GetCancellationToken(Guid jobId)
    {
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            return cts.Token;
        }

        return CancellationToken.None;
    }

    private void CleanupExpiredJobs(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredJobs = _progressCache
            .Where(kvp => (now - kvp.Value.UpdatedAt) > _jobTtl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var jobId in expiredJobs)
        {
            _progressCache.TryRemove(jobId, out _);
            
            if (_cancellationTokens.TryRemove(jobId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        foreach (var cts in _cancellationTokens.Values)
        {
            cts.Dispose();
        }
        
        _cancellationTokens.Clear();
        _progressCache.Clear();
    }
}
