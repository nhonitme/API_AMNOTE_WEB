using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;

 	/// <summary>
    /// Lưu progress đang chạy trong RAM để API getProgress không phải đọc DB liên tục.
    /// DB chỉ nên lưu trạng thái cuối cùng DONE/ERROR và thông tin job ban đầu.
    /// Lưu ý: store này phù hợp khi API chạy 1 instance. Nếu scale nhiều instance, nên đổi sang Redis/IDistributedCache.
    /// </summary>

public static class PeriodLockProgressMemoryStore
{
    private sealed class ProgressEntry
    {
        public PeriodLockProgressDto Progress { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    private static readonly ConcurrentDictionary<string, ProgressEntry> Items = new();

    private static string BuildKey(string companyCd, string jobId)
    {
        return $"{companyCd ?? string.Empty}:{jobId ?? string.Empty}";
    }

    public static void Set(string companyCd, PeriodLockProgressDto progress)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || progress == null || string.IsNullOrWhiteSpace(progress.JobId))
        {
            return;
        }

        Items[BuildKey(companyCd, progress.JobId)] = new ProgressEntry
        {
            Progress = Clone(progress),
            UpdatedAt = DateTime.Now
        };
    }

    public static PeriodLockProgressDto? Get(string companyCd, string jobId)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        if (!Items.TryGetValue(BuildKey(companyCd, jobId), out var entry))
        {
            return null;
        }

        return Clone(entry.Progress);
    }

    public static void Remove(string companyCd, string jobId)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        Items.TryRemove(BuildKey(companyCd, jobId), out _);
    }

    public static void CleanupFinished(TimeSpan maxAge)
    {
        BackgroundJobMemoryCleanup.Cleanup(
            Items,
            maxAge,
            entry => entry.Progress.Status == PeriodLockJobStatus.Done
                || entry.Progress.Status == PeriodLockJobStatus.Error,
            entry => entry.UpdatedAt);
    }

    private static PeriodLockProgressDto Clone(PeriodLockProgressDto source)
    {
        return new PeriodLockProgressDto
        {
            JobId = source.JobId,
            Percent = source.Percent,
            TotalSteps = source.TotalSteps,
            DoneSteps = source.DoneSteps,
            CurrentPeriodYm = source.CurrentPeriodYm,
            CurrentPeriodLabel = source.CurrentPeriodLabel,
            CurrentStepCode = source.CurrentStepCode,
            CurrentStepName = source.CurrentStepName,
            TargetStepCode = source.TargetStepCode,
            TargetStepName = source.TargetStepName,
            Message = source.Message,
            Status = source.Status
        };
    }
}
