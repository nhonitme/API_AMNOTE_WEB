using System.Threading;
using System.Threading.Tasks;
namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class ExcelImportJobProgressWriter : IExcelImportJobProgressWriter
{
    private readonly string _jobId;
    private readonly IExcelImportJobStore _jobStore;

    public ExcelImportJobProgressWriter(string jobId, IExcelImportJobStore jobStore)
    {
        _jobId = jobId;
        _jobStore = jobStore;
    }

    public Task ReportAsync(int processedRows, int totalRows, string? message = null, CancellationToken cancellationToken = default)
    {
        _jobStore.UpdateProgress(_jobId, processedRows, totalRows, message);
        return Task.CompletedTask;
    }

    public Task ReportAsync(int processedRows, int totalRows, int percent, string? message = null, CancellationToken cancellationToken = default)
    {
        _jobStore.UpdateProgress(_jobId, processedRows, totalRows, message, percent);
        return Task.CompletedTask;
    }

    public Task ReportPercentAsync(int percent, string? message = null, CancellationToken cancellationToken = default)
    {
        _jobStore.UpdatePercent(_jobId, percent, message);
        return Task.CompletedTask;
    }
}
