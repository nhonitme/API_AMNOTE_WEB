using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.InventoryValuation;

public sealed class InventoryValuationJobStore
{
    private readonly ConcurrentDictionary<string, InventoryValuationJobState> _jobs = new();
    private readonly BackgroundJobQueue<InventoryValuationJobRequest> _queue = new();

    public async Task<InventoryValuationStartResultDto> StartAsync(
        string companyCd,
        string databaseName,
        string fromYmd,
        string toYmd,
        string methodCode,
        string? productCds,
        string? storeCds,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedMethodCode = InventoryValuationMethod.Normalize(methodCode);
        var jobId = Guid.NewGuid().ToString("N");

        var request = new InventoryValuationJobRequest
        {
            JobId = jobId,
            CompanyCd = companyCd,
            DatabaseName = databaseName,
            FromYmd = fromYmd,
            ToYmd = toYmd,
            MethodCode = normalizedMethodCode,
            ProductCds = productCds,
            StoreCds = storeCds,
            UserId = userId,
            CreatedAt = DateTime.Now
        };

        _jobs[jobId] = new InventoryValuationJobState
        {
            JobId = jobId,
            CompanyCd = companyCd,
            FromYmd = fromYmd,
            ToYmd = toYmd,
            MethodCode = normalizedMethodCode,
            MethodName = InventoryValuationMethod.GetDisplayName(normalizedMethodCode),
            Status = BackgroundJobStatus.Queued,
            Message = "Đã đưa yêu cầu tính giá xuất kho vào hàng đợi.",
            CreatedAt = request.CreatedAt,
            UpdatedAt = DateTime.Now
        };

        await _queue.EnqueueAsync(request, cancellationToken);

        return new InventoryValuationStartResultDto
        {
            JobId = jobId,
            Status = BackgroundJobStatus.Queued,
            Message = "Đã tạo job tính giá xuất kho."
        };
    }

    public ValueTask<InventoryValuationJobRequest> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.DequeueAsync(cancellationToken);
    }

    public ValueTask RequeueAsync(InventoryValuationJobRequest request, CancellationToken cancellationToken = default)
    {
        return _queue.EnqueueAsync(request, cancellationToken);
    }

    public InventoryValuationJobState? Get(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var state) ? state : null;
    }

    public InventoryValuationProgressDto? GetProgress(string jobId)
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

    public void UpdateProgress(string jobId, int processedGroups, int totalGroups, int movementCount, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Processing;
        state.ProcessedGroups = Math.Max(0, processedGroups);
        state.TotalGroups = Math.Max(0, totalGroups);
        state.MovementCount = Math.Max(0, movementCount);
        state.Percent = BackgroundJobProgressHelper.CalculatePercent(processedGroups, totalGroups, state.Percent);
        state.Message = message;
        state.UpdatedAt = DateTime.Now;
    }

    public void Complete(string jobId, int processedGroups, int movementCount, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Done;
        state.ProcessedGroups = processedGroups;
        state.TotalGroups = processedGroups;
        state.MovementCount = movementCount;
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

    private static InventoryValuationProgressDto MapProgress(InventoryValuationJobState state)
    {
        return new InventoryValuationProgressDto
        {
            JobId = state.JobId,
            Status = state.Status,
            Percent = state.Percent,
            ProcessedGroups = state.ProcessedGroups,
            TotalGroups = state.TotalGroups,
            MovementCount = state.MovementCount,
            MethodCode = state.MethodCode,
            MethodName = state.MethodName,
            Message = state.Message,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt
        };
    }
}
