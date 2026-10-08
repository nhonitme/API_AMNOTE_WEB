using API_AMNOTE_WEB.Models;
using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

public static class BackgroundJobMemoryCleanup
{
    public static int Cleanup<TKey, TJob>(
        ConcurrentDictionary<TKey, TJob> jobs,
        TimeSpan keepDuration,
        Func<TJob, bool> isFinished,
        Func<TJob, DateTime> getLastActivityAt)
        where TKey : notnull
    {
        var now = DateTime.Now;
        var removedCount = 0;

        foreach (var item in jobs)
        {
            var job = item.Value;

            if (!isFinished(job))
            {
                continue;
            }

            if (now - getLastActivityAt(job) > keepDuration && jobs.TryRemove(item.Key, out _))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    public static bool HasCleanupCandidates<TKey, TJob>(
        ConcurrentDictionary<TKey, TJob> jobs,
        TimeSpan keepDuration,
        Func<TJob, bool> isFinished,
        Func<TJob, DateTime> getLastActivityAt)
        where TKey : notnull
    {
        if (jobs.IsEmpty)
        {
            return false;
        }

        var now = DateTime.Now;

        return jobs.Values.Any(job =>
            isFinished(job) && now - getLastActivityAt(job) > keepDuration);
    }

    public static async Task RunPeriodicLoopAsync(
        Func<bool> hasCandidates,
        Func<int> cleanup,
        TimeSpan interval,
        ILogger logger,
        string jobTypeName,
        CancellationToken stoppingToken,
        bool runImmediately = false)
    {
        if (runImmediately && hasCandidates())
        {
            var initialRemoved = cleanup();
            if (initialRemoved > 0)
            {
                logger.LogInformation("Cleaned {RemovedCount} completed {JobType} job(s).", initialRemoved, jobTypeName);
            }
        }

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);

                if (!hasCandidates())
                {
                    continue;
                }

                var removedCount = cleanup();
                if (removedCount > 0)
                {
                    logger.LogInformation("Cleaned {RemovedCount} completed {JobType} job(s).", removedCount, jobTypeName);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cleanup {JobType} jobs failed.", jobTypeName);
            }
        }
    }
}
