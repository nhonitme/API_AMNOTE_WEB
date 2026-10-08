using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.CashExchangeRevaluation;

public sealed class CashExchangeRevaluationJobStore
{
    private readonly ConcurrentDictionary<string, CashExchangeRevaluationJobState> _jobs = new();
    private readonly BackgroundJobQueue<CashExchangeRevaluationJobRequest> _queue = new();

    public async Task<CashExchangeRevaluationStartResultDto> StartAsync(
        CashExchangeRevaluationJobRequest request,
        CancellationToken cancellationToken = default)
    {
        _jobs[request.JobId] = new CashExchangeRevaluationJobState
        {
            JobId = request.JobId,
            CompanyCd = request.CompanyCd,
            Status = BackgroundJobStatus.Queued,
            Message = "Đã đưa yêu cầu tính lại tỷ giá xuất quỹ vào hàng đợi.",
            CreatedAt = request.CreatedAt,
            UpdatedAt = DateTime.Now
        };

        await _queue.EnqueueAsync(request, cancellationToken);

        return new CashExchangeRevaluationStartResultDto
        {
            JobId = request.JobId,
            Status = BackgroundJobStatus.Queued,
            Message = "Đã tạo job tính lại tỷ giá xuất quỹ."
        };
    }

    public ValueTask<CashExchangeRevaluationJobRequest> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.DequeueAsync(cancellationToken);
    }

    public ValueTask RequeueAsync(CashExchangeRevaluationJobRequest request, CancellationToken cancellationToken = default)
    {
        return _queue.EnqueueAsync(request, cancellationToken);
    }

    public CashExchangeRevaluationJobState? Get(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var state) ? state : null;
    }

    public CashExchangeRevaluationProgressDto? GetProgress(string jobId)
    {
        var state = Get(jobId);
        return state == null ? null : MapProgress(state);
    }

    public void MarkProcessing(string jobId, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Processing;
        state.Percent = Math.Max(state.Percent, 5);
        state.Message = message;
        state.UpdatedAt = DateTime.Now;
    }

    public void UpdateProgress(string jobId, int processed, int total, int savedCount, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Processing;
        state.SavedCount = Math.Max(0, savedCount);
        state.Percent = BackgroundJobProgressHelper.CalculatePercent(processed, total, state.Percent);
        state.Message = message;
        state.UpdatedAt = DateTime.Now;
    }

    public void Complete(string jobId, int savedCount, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Done;
        state.SavedCount = savedCount;
        state.Percent = 100;
        state.Message = message;
        state.UpdatedAt = DateTime.Now;
    }

    public void Fail(string jobId, string message)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Error;
        state.Percent = 100;
        state.Message = message;
        state.UpdatedAt = DateTime.Now;
    }

    public int CleanupCompletedJobs(TimeSpan keepDuration)
    {
        return BackgroundJobMemoryCleanup.Cleanup(
            _jobs,
            keepDuration,
            job => BackgroundJobStatus.IsFinished(job.Status),
            job => job.UpdatedAt ?? job.CreatedAt);
    }

    public bool HasCleanupCandidates(TimeSpan keepDuration)
    {
        return BackgroundJobMemoryCleanup.HasCleanupCandidates(
            _jobs,
            keepDuration,
            job => BackgroundJobStatus.IsFinished(job.Status),
            job => job.UpdatedAt ?? job.CreatedAt);
    }

    private static CashExchangeRevaluationProgressDto MapProgress(CashExchangeRevaluationJobState state)
    {
        return new CashExchangeRevaluationProgressDto
        {
            JobId = state.JobId,
            Status = state.Status,
            Percent = state.Percent,
            SavedCount = state.SavedCount,
            Message = state.Message,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt
        };
    }
}
