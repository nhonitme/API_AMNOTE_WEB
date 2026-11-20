namespace API_AMNOTE_WEB.Services;

public interface IEElectronicDocumentPdfWorkSignal
{
    void NotifyWorkAvailable();

    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
