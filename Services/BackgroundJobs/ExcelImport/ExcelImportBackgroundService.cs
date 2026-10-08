using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class ExcelImportBackgroundService : BackgroundService
{
    // Keep at 1: convert uploads large masters sequentially; multi-worker races HttpContextAccessor
    // and amplifies MySQL sequence locks. Parallelism here caused opaque 502 / process restarts.
    private const int WorkerCount = 1;
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan KeepCompletedJobDuration = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExcelImportJobQueue _queue;
    private readonly IExcelImportJobStore _jobStore;
    private readonly IExcelImportJobCancellationRegistry _cancellationRegistry;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExcelImportBackgroundService> _logger;

    public ExcelImportBackgroundService(
        IServiceScopeFactory scopeFactory,
        IExcelImportJobQueue queue,
        IExcelImportJobStore jobStore,
        IExcelImportJobCancellationRegistry cancellationRegistry,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ExcelImportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _jobStore = jobStore;
        _cancellationRegistry = cancellationRegistry;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Excel import background service started. WorkerCount={WorkerCount}", WorkerCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workers = Enumerable
                    .Range(1, WorkerCount)
                    .Select(workerNo => RunWorkerAsync(workerNo, stoppingToken));

                await Task.WhenAll(workers.Append(RunCleanupLoopAsync(stoppingToken)));
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel import background service crashed; restarting workers in 3s.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task RunWorkerAsync(int workerNo, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ExcelImportJobRequest request;

            try
            {
                request = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Do not pass stoppingToken into job body — finish in-flight import even if host is stopping.
            await ProcessOneJobAsync(request, workerNo);
        }
    }

    private async Task ProcessOneJobAsync(ExcelImportJobRequest request, int workerNo)
    {
        using var timeoutCts = new CancellationTokenSource(JobTimeout);
        var jobToken = _cancellationRegistry.Register(request.JobId, timeoutCts.Token);

        try
        {
            // User Cancel while QUEUED — skip without MarkProcessing.
            var existing = _jobStore.Get(request.JobId);
            if (existing == null || BackgroundJobStatus.IsFinished(existing.Status))
            {
                _logger.LogInformation(
                    "Excel import job skipped (already finished/cancelled). JobId={JobId}, Status={Status}",
                    request.JobId,
                    existing?.Status ?? "(missing)");
                return;
            }

            _jobStore.MarkProcessing(
                request.JobId,
                $"Worker {workerNo}: Bắt đầu xử lý import Excel."
            );

            // Cancel won the race against MarkProcessing.
            existing = _jobStore.Get(request.JobId);
            if (existing == null || BackgroundJobStatus.IsFinished(existing.Status))
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            using var httpContextScope = UseFakeHttpContext(request, scope.ServiceProvider);

            var processor = scope.ServiceProvider.GetRequiredService<IExcelImportJobProcessor>();
            var handlerRegistry = scope.ServiceProvider.GetRequiredService<IExcelImportModuleHandlerRegistry>();
            var handler = handlerRegistry.GetHandler(request.ModuleCd);

            var progressWriter = new ExcelImportJobProgressWriter(request.JobId, _jobStore);

            // WhenAny: MySQL/SP sometimes ignore CancellationToken — still free the single worker.
            var processTask = processor.ProcessAsync(
                request,
                progressWriter,
                handler,
                jobToken);
            var delayTask = Task.Delay(JobTimeout, CancellationToken.None);
            var finished = await Task.WhenAny(processTask, delayTask);
            if (finished != processTask)
            {
                timeoutCts.Cancel();
                throw new OperationCanceledException("Job import Excel quá thời gian xử lý cho phép.");
            }

            var result = await processTask;

            _jobStore.Complete(request.JobId, result);

            var notificationService = scope.ServiceProvider.GetService<ISysNotificationService>();
            if (notificationService != null && !string.IsNullOrWhiteSpace(request.UserId))
            {
                var isSuccess = result?.Success == true;
                await NotifyWithTimeoutAsync(
                    () => notificationService.NotifyJobResultAsync(
                        request.CompanyCd,
                        request.UserId,
                        isSuccess ? "EXCEL_IMPORT_DONE" : "EXCEL_IMPORT_FAILED",
                        "EXCEL_IMPORT",
                        request.JobId,
                        isSuccess ? "Import Excel hoàn tất" : "Import Excel bị lỗi",
                        result?.Message ?? (isSuccess ? "Import Excel đã hoàn tất." : "Import Excel thất bại."),
                        isSuccess ? "LOW" : "HIGH",
                        "/excel-import"),
                    request.JobId,
                    _logger);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            var state = _jobStore.Get(request.JobId);
            if (state != null && state.Status == BackgroundJobStatus.Cancelled)
            {
                return;
            }

            _jobStore.Fail(request.JobId, "Job import Excel quá thời gian xử lý cho phép.");
            await TryNotifyExcelImportFailureAsync(_scopeFactory, request, "Job import Excel quá thời gian xử lý cho phép.");
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogInformation(
                oce,
                "Excel import job cancelled. WorkerNo={WorkerNo}, JobId={JobId}",
                workerNo,
                request.JobId);
            _jobStore.TryCancel(
                request.JobId,
                ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_CANCELLED", request.Lang));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Excel import job failed. WorkerNo={WorkerNo}, JobId={JobId}",
                workerNo,
                request.JobId);

            _jobStore.Fail(request.JobId, ex.Message);
            await TryNotifyExcelImportFailureAsync(_scopeFactory, request, ex.Message);
        }
        finally
        {
            _cancellationRegistry.Unregister(request.JobId);
            TryDeleteFile(request.TempFilePath);
        }
    }

    private Task RunCleanupLoopAsync(CancellationToken stoppingToken)
    {
        return BackgroundJobMemoryCleanup.RunPeriodicLoopAsync(
            () => _jobStore.HasCleanupCandidates(KeepCompletedJobDuration),
            () => _jobStore.CleanupCompletedJobs(KeepCompletedJobDuration),
            CleanupInterval,
            _logger,
            "Excel import",
            stoppingToken,
            runImmediately: true);
    }

    private static async Task TryNotifyExcelImportFailureAsync(
        IServiceScopeFactory scopeFactory,
        ExcelImportJobRequest request,
        string message)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetService<ISysNotificationService>();
        if (notificationService == null)
        {
            return;
        }

        await NotifyWithTimeoutAsync(
            () => notificationService.NotifyJobResultAsync(
                request.CompanyCd,
                request.UserId,
                "EXCEL_IMPORT_FAILED",
                "EXCEL_IMPORT",
                request.JobId,
                "Import Excel bị lỗi",
                message,
                "HIGH",
                "/excel-import"),
            request.JobId,
            null);
    }

    /// <summary>
    /// Never block the single excel-import worker on notification I/O.
    /// </summary>
    private static async Task NotifyWithTimeoutAsync(
        Func<Task> notifyAsync,
        string jobId,
        ILogger? logger)
    {
        try
        {
            var notifyTask = notifyAsync();
            var finished = await Task.WhenAny(notifyTask, Task.Delay(TimeSpan.FromSeconds(5)));
            if (finished != notifyTask)
            {
                logger?.LogWarning("Excel import notification timed out after 5s. JobId={JobId}", jobId);
                return;
            }

            await notifyTask;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Excel import notification failed. JobId={JobId}", jobId);
        }
    }

    private IDisposable UseFakeHttpContext(ExcelImportJobRequest request, IServiceProvider serviceProvider)
    {
        var previousContext = _httpContextAccessor.HttpContext;

        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, request.UserId));
            claims.Add(new Claim(ClaimTypes.Name, request.UserId));
        }

        var fakeHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ExcelImportJob")),
            RequestServices = serviceProvider
        };

        // Prevent accidental cancel via RequestAborted (DefaultHttpContext lifetime quirks).
        fakeHttpContext.Features.Set<IHttpRequestLifetimeFeature>(new HttpRequestLifetimeFeature());

        if (!string.IsNullOrWhiteSpace(request.CompanyCd)
            && CompanyRouteContextMiddleware.IsValidCompanyCode(request.CompanyCd))
        {
            fakeHttpContext.Items[CompanyRouteContextMiddleware.CompanyCdItemKey] = request.CompanyCd.Trim();
            fakeHttpContext.Request.Headers[CompanyRouteContextMiddleware.CompanyCdHeaderName] = request.CompanyCd.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.UserIp))
        {
            fakeHttpContext.Request.Headers[ClientPublicIpResolver.ClientIpHeader] = request.UserIp.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.UserAgent))
        {
            fakeHttpContext.Request.Headers.UserAgent = request.UserAgent.Trim();
        }

        _httpContextAccessor.HttpContext = fakeHttpContext;

        return new DelegateDisposable(() =>
        {
            _httpContextAccessor.HttpContext = previousContext;
        });
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
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
