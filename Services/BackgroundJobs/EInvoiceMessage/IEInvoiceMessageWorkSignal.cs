namespace API_AMNOTE_WEB.Services.BackgroundJobs.EInvoiceMessage;

/// <summary>
/// Wakes the e-invoice message worker when outbound work is enqueued.
/// Worker stays idle (no DB polling) until signaled, then drains until queues are empty.
/// </summary>
public interface IEInvoiceMessageWorkSignal
{
    void NotifyWorkAvailable();

    Task WaitAsync(CancellationToken cancellationToken);
}
