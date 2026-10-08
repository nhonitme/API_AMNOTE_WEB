using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock
{
    public sealed class PeriodLockBackgroundService : BackgroundService
    {
        private const int WorkerCount = 2;
        private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(60);

        private readonly IPeriodLockJobQueue _queue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CompanyKeyLocker _locker;
        private readonly ILogger<PeriodLockBackgroundService> _logger;

        public PeriodLockBackgroundService(
            IPeriodLockJobQueue queue,
            IServiceScopeFactory serviceScopeFactory,
            IHttpContextAccessor httpContextAccessor,
            CompanyKeyLocker locker,
            ILogger<PeriodLockBackgroundService> logger)
        {
            _queue = queue;
            _serviceScopeFactory = serviceScopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _locker = locker;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "PeriodLock background service started. WorkerCount={WorkerCount}",
                WorkerCount
            );

            var workers = Enumerable
                .Range(1, WorkerCount)
                .Select(workerNo => RunWorkerAsync(workerNo, stoppingToken));

            await Task.WhenAll(workers);
        }

        private async Task RunWorkerAsync(
            int workerNo,
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "PeriodLock worker {WorkerNo} started.",
                workerNo
            );

            await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
            {
                await ProcessJobAsync(job, workerNo, stoppingToken);
            }
        }

        private async Task ProcessJobAsync(
            PeriodLockJobItem job,
            int workerNo,
            CancellationToken stoppingToken)
        {
            var lockKey = BuildLockKey(job);

            if (!_locker.TryEnter(lockKey, out var lockHandle))
            {
                // Cùng công ty đang có job khác chạy.
                // Đưa lại về cuối queue để worker không bị đứng chờ.
                await _queue.EnqueueAsync(job, stoppingToken);
                await Task.Delay(500, stoppingToken);
                return;
            }

            using (lockHandle!)
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var repo = scope.ServiceProvider.GetRequiredService<IPeriodLockRepository>();

                using var httpContextScope = UseFakeHttpContext(
                    job,
                    scope.ServiceProvider
                );

                using var timeoutCts = CancellationTokenSource
                    .CreateLinkedTokenSource(stoppingToken);

                timeoutCts.CancelAfter(JobTimeout);

                try
                {
                    if (job.JobType == PeriodLockJobType.Lock)
                    {
                        if (job.LockRequest == null)
                            throw new InvalidOperationException("LockRequest is required");

                        await repo.RunJobAsync(
                            job.CompanyCd,
                            job.JobId,
                            job.LockRequest,
                            job.UserId,
                            job.DatabaseName,
                            timeoutCts.Token
                        );

                        return;
                    }

                    if (job.JobType == PeriodLockJobType.Unlock)
                    {
                        if (job.UnlockRequest == null)
                            throw new InvalidOperationException("UnlockRequest is required");

                        await repo.RunUnlockJobAsync(
                            job.CompanyCd,
                            job.JobId,
                            job.UnlockRequest,
                            job.UserId,
                            job.DatabaseName,
                            timeoutCts.Token
                        );

                        return;
                    }

                    throw new InvalidOperationException($"Unknown PeriodLock job type: {job.JobType}");
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    await MarkJobErrorAsync(
                        repo,
                        job,
                        $"Job khóa/mở sổ quá thời gian xử lý cho phép: {JobTimeout.TotalMinutes} phút."
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "PeriodLock background job failed. JobId={JobId}, JobType={JobType}",
                        job.JobId,
                        job.JobType
                    );

                    await MarkJobErrorAsync(
                        repo,
                        job,
                        $"Lỗi background job: {ex.Message}"
                    );
                }
            }
        }

        private static string BuildLockKey(PeriodLockJobItem job)
        {
            return job.CompanyCd;            
        }

        private IDisposable UseFakeHttpContext(
            PeriodLockJobItem job,
            IServiceProvider serviceProvider)
        {
            var previousContext = _httpContextAccessor.HttpContext;

            var fakeHttpContext = new DefaultHttpContext
            {
                User = job.UserPrincipal ?? new ClaimsPrincipal(),
                RequestServices = serviceProvider
            };

            if (!string.IsNullOrWhiteSpace(job.AuthorizationHeader))
            {
                fakeHttpContext.Request.Headers.Authorization = job.AuthorizationHeader;
            }

            _httpContextAccessor.HttpContext = fakeHttpContext;

            return new DelegateDisposable(() =>
            {
                _httpContextAccessor.HttpContext = previousContext;
            });
        }

        private async Task MarkJobErrorAsync(
            IPeriodLockRepository repo,
            PeriodLockJobItem job,
            string errorMessage)
        {
            try
            {
                var currentProgress = await repo.GetJobProgressAsync(
                    job.CompanyCd,
                    job.JobId
                );

                await repo.UpdateJobProgressAsync(
                    job.CompanyCd,
                    job.JobId,
                    currentProgress?.DoneSteps ?? 0,
                    currentProgress?.TotalSteps ?? 0,
                    currentProgress?.CurrentPeriodYm ?? string.Empty,
                    currentProgress?.CurrentStepCode ?? string.Empty,
                    currentProgress?.CurrentStepName ?? "-",
                    errorMessage,
                    PeriodLockJobStatus.Error,
                    job.DatabaseName
                );
            }
            catch (Exception updateEx)
            {
                _logger.LogError(
                    updateEx,
                    "Update PeriodLock job ERROR status failed. JobId={JobId}",
                    job.JobId
                );
            }
        }

        private sealed class DelegateDisposable : IDisposable
        {
            private readonly Action _dispose;

            public DelegateDisposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose();
            }
        }
    }
}