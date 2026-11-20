namespace API_AMNOTE_WEB.Services;

public sealed class ElectronicDocumentPdfWorkSignal : IEElectronicDocumentPdfWorkSignal
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
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var acquired = await _signal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (acquired)
        {
            Interlocked.Exchange(ref _pending, 0);
        }
    }
}
