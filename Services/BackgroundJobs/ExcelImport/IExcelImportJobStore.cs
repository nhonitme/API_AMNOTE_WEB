namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public interface IExcelImportJobStore
{
    ExcelImportJobState Create(ExcelImportJobRequest request);
    ExcelImportJobState? Get(string jobId);
    ExcelImportProgressDto? GetProgress(string jobId);
    void MarkProcessing(string jobId, string? message = null);
    void UpdateProgress(string jobId, int processedRows, int totalRows, string? message = null, int? percent = null);
    void UpdatePercent(string jobId, int percent, string? message = null);
    void Complete(string jobId, ExcelImportResultDto result);
    void Fail(string jobId, string message);

    /// <summary>
    /// Mark job CANCELLED if still running/queued. Returns false when job missing or already finished.
    /// </summary>
    bool TryCancel(string jobId, string? message = null);

    int CleanupCompletedJobs(TimeSpan keepDuration);

    bool HasCleanupCandidates(TimeSpan keepDuration);
}
