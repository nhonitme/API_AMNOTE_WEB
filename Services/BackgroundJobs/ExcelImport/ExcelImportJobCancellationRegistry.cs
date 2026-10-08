using System.Collections.Concurrent;
using System.Threading;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class ExcelImportJobCancellationRegistry : IExcelImportJobCancellationRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokens =
        new(StringComparer.OrdinalIgnoreCase);

    public CancellationToken Register(string jobId, CancellationToken timeoutToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken);
        if (_tokens.TryAdd(jobId, linked))
        {
            return linked.Token;
        }

        // Replace stale registration (should be rare).
        if (_tokens.TryRemove(jobId, out var previous))
        {
            previous.Dispose();
        }

        _tokens[jobId] = linked;
        return linked.Token;
    }

    public bool TryCancel(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return false;
        }

        if (!_tokens.TryGetValue(jobId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Unregister(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        if (_tokens.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }
}
