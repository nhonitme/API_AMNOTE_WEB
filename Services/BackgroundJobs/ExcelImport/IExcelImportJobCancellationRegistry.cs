using System.Threading;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

/// <summary>
/// Per-job cancel tokens for in-flight excel import workers.
/// Cancel is explicit (user Cancel Import) — not tied to HTTP request lifetime.
/// </summary>
public interface IExcelImportJobCancellationRegistry
{
    /// <summary>
    /// Register a linked token for an in-flight job. Caller must <see cref="Unregister"/> in finally.
    /// </summary>
    CancellationToken Register(string jobId, CancellationToken timeoutToken);

    /// <summary>
    /// Signal cancel for a processing job. No-op if job is not registered (still queued / already done).
    /// </summary>
    bool TryCancel(string jobId);

    void Unregister(string jobId);
}
