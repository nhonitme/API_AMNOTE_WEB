using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class ExcelImportJobQueue : IExcelImportJobQueue
{
    private readonly BackgroundJobQueue<ExcelImportJobRequest> _queue = new();

    public ValueTask EnqueueAsync(ExcelImportJobRequest request, CancellationToken cancellationToken = default)
    {
        return _queue.EnqueueAsync(request, cancellationToken);
    }

    public ValueTask<ExcelImportJobRequest> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.DequeueAsync(cancellationToken);
    }
}
