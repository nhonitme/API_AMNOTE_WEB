using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

public sealed class CompanyKeyLocker
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public bool TryEnter(string key, out IDisposable? releaser)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        if (!semaphore.Wait(0))
        {
            releaser = null;
            return false;
        }

        releaser = new LockReleaser(semaphore);
        return true;
    }

    private sealed class LockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public LockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}
