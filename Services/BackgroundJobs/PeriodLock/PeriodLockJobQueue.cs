using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;

public sealed class PeriodLockJobQueue : IPeriodLockJobQueue
{
    private const int QueueCapacity = 5000;
    private readonly BackgroundJobQueue<PeriodLockJobItem> _queue = new(QueueCapacity);

    public Task EnqueueAsync(PeriodLockJobItem jobItem, CancellationToken cancellationToken = default)
    {
        if (jobItem == null)
        {
            throw new ArgumentNullException(nameof(jobItem));
        }

        return _queue.EnqueueAsync(jobItem, cancellationToken).AsTask();
    }

    public IAsyncEnumerable<PeriodLockJobItem> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return _queue.ReadAllAsync(cancellationToken);
    }
}
