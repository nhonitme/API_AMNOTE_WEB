namespace API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;

public interface IPeriodLockJobQueue
{
 
        /// </summary>
    Task EnqueueAsync(PeriodLockJobItem jobItem, CancellationToken cancellationToken = default);
 
    IAsyncEnumerable<PeriodLockJobItem> DequeueAllAsync(CancellationToken cancellationToken = default);
}
