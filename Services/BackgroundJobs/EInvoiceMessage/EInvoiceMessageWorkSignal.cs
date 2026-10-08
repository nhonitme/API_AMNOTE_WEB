namespace API_AMNOTE_WEB.Services.BackgroundJobs.EInvoiceMessage;

public sealed class EInvoiceMessageWorkSignal : IEInvoiceMessageWorkSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private int _pending;

    public void NotifyWorkAvailable()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signaled; coalesced wake is enough.
        }
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _pending, 0);
    }
}
