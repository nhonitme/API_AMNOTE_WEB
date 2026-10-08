using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.CashExchangeRevaluation;

public sealed class CashExchangeRevaluationJobWorker : BackgroundService
{
    private const int WorkerCount = 2;
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan KeepCompletedJobDuration = TimeSpan.FromHours(2);

    private readonly CashExchangeRevaluationJobStore _jobStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CompanyKeyLocker _locker;
    private readonly ILogger<CashExchangeRevaluationJobWorker> _logger;

    public CashExchangeRevaluationJobWorker(
        CashExchangeRevaluationJobStore jobStore,
        IServiceScopeFactory scopeFactory,
        CompanyKeyLocker locker,
        ILogger<CashExchangeRevaluationJobWorker> logger)
    {
        _jobStore = jobStore;
        _scopeFactory = scopeFactory;
        _locker = locker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cash exchange revaluation job worker started. WorkerCount={WorkerCount}", WorkerCount);

        var workers = Enumerable
            .Range(1, WorkerCount)
            .Select(workerNo => RunWorkerAsync(workerNo, stoppingToken));

        await Task.WhenAll(workers.Append(RunCleanupLoopAsync(stoppingToken)));
    }

    private async Task RunWorkerAsync(int workerNo, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CashExchangeRevaluationJobRequest request;

            try
            {
                request = await _jobStore.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessJobAsync(request, workerNo, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(
        CashExchangeRevaluationJobRequest request,
        int workerNo,
        CancellationToken stoppingToken)
    {
        if (!_locker.TryEnter(request.CompanyCd, out var lockHandle))
        {
            await _jobStore.RequeueAsync(request, stoppingToken);
            await Task.Delay(500, stoppingToken);
            return;
        }

        using (lockHandle!)
        {
            _jobStore.MarkProcessing(request.JobId, $"Worker {workerNo}: Đang tính lại tỷ giá xuất quỹ...");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(JobTimeout);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ICashExchangeRevaluationService>();

                var result = await service.SaveAsync(
                    request.CompanyCd,
                    request.UserId,
                    request.DatabaseName ?? string.Empty,
                    request.RateDate,
                    request.FcType,
                    request.ChitYmdFrom,
                    request.ChitYmdTo,
                    request.RateMethod,
                    (processed, total, message) =>
                    {
                        _jobStore.UpdateProgress(request.JobId, processed, total, 0, message);
                    },
                    timeoutCts.Token);

                _jobStore.Complete(
                    request.JobId,
                    result.SAVED_COUNT,
                    result.SAVED_COUNT > 0
                        ? $"Tính lại tỷ giá hoàn tất ({result.SAVED_COUNT} dòng)."
                        : "Không có dòng nào được lưu.");
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _jobStore.Fail(request.JobId, $"Job tính lại tỷ giá xuất quỹ quá thời gian xử lý cho phép: {JobTimeout.TotalMinutes} phút.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cash exchange revaluation job failed. JobId={JobId}, CompanyCd={CompanyCd}", request.JobId, request.CompanyCd);
                _jobStore.Fail(request.JobId, ex.Message);
            }
        }
    }

    private Task RunCleanupLoopAsync(CancellationToken stoppingToken)
    {
        return BackgroundJobMemoryCleanup.RunPeriodicLoopAsync(
            () => _jobStore.HasCleanupCandidates(KeepCompletedJobDuration),
            () => _jobStore.CleanupCompletedJobs(KeepCompletedJobDuration),
            CleanupInterval,
            _logger,
            "cash exchange revaluation",
            stoppingToken);
    }
}
