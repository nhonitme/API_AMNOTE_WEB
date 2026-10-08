using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.InventoryValuation;

public sealed class InventoryValuationJobWorker : BackgroundService
{
    private const int WorkerCount = 2;
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan KeepCompletedJobDuration = TimeSpan.FromHours(2);

    private readonly InventoryValuationJobStore _jobStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CompanyKeyLocker _locker;
    private readonly ILogger<InventoryValuationJobWorker> _logger;

    public InventoryValuationJobWorker(
        InventoryValuationJobStore jobStore,
        IServiceScopeFactory scopeFactory,
        CompanyKeyLocker locker,
        ILogger<InventoryValuationJobWorker> logger)
    {
        _jobStore = jobStore;
        _scopeFactory = scopeFactory;
        _locker = locker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inventory valuation job worker started. WorkerCount={WorkerCount}", WorkerCount);

        var workers = Enumerable
            .Range(1, WorkerCount)
            .Select(workerNo => RunWorkerAsync(workerNo, stoppingToken));

        await Task.WhenAll(workers.Append(RunCleanupLoopAsync(stoppingToken)));
    }

    private async Task RunWorkerAsync(int workerNo, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            InventoryValuationJobRequest request;

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
        InventoryValuationJobRequest request,
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
            _jobStore.MarkProcessing(request.JobId, $"Worker {workerNo}: Đang tính giá xuất kho...");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(JobTimeout);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IInventoryValuationService>();

                var result = await service.CalculateAsync(
                    request.CompanyCd,
                    request.FromYmd,
                    request.ToYmd,
                    request.MethodCode,
                    request.DatabaseName,
                    request.ProductCds,
                    request.StoreCds,
                    (processedGroups, totalGroups, movementCount, message) =>
                    {
                        _jobStore.UpdateProgress(request.JobId, processedGroups, totalGroups, movementCount, message);
                    },
                    timeoutCts.Token);

                _jobStore.Complete(
                    request.JobId,
                    result.ProcessedGroups,
                    result.MovementCount,
                    $"Tính toán hoàn tất ({result.ProcessedGroups} nhóm, {result.MovementCount} dòng phát sinh).");
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _jobStore.Fail(request.JobId, $"Job tính giá xuất kho quá thời gian xử lý cho phép: {JobTimeout.TotalMinutes} phút.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventory valuation job failed. JobId={JobId}, CompanyCd={CompanyCd}", request.JobId, request.CompanyCd);
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
            "inventory valuation",
            stoppingToken);
    }
}
