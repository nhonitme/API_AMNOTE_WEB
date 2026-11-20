using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.TaxWithholding.Interfaces;

namespace API_AMNOTE_WEB.Services;

public sealed class ElectronicDocumentPdfWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    IEElectronicDocumentPdfWorkSignal workSignal,
    ILogger<ElectronicDocumentPdfWorker> logger) : BackgroundService
{
    private sealed class Target
    {
        public string COMPANY_CD { get; set; } = "";
        public string DB_NAME { get; set; } = "";
    }

    private sealed class Pending
    {
        public string DOCUMENT_KIND { get; set; } = "";
        public long DOCUMENT_ID { get; set; }
        public long SOURCE_REVISION { get; set; }
    }

    private sealed class DrainResult
    {
        public bool HadDueWork { get; init; }
        public TimeSpan? NextAttemptDelay { get; init; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("ElectronicDocuments:WorkerEnabled", true))
        {
            logger.LogInformation("Electronic document PDF worker is disabled.");
            return;
        }

        var recovery = TimeSpan.FromSeconds(Math.Max(30, configuration.GetValue("ElectronicDocuments:RecoveryPollSeconds", 300)));
        var drainIdleDelay = TimeSpan.FromMilliseconds(Math.Max(0, configuration.GetValue("ElectronicDocuments:DrainIdleDelayMs", 200)));

        logger.LogInformation(
            "Electronic document PDF worker started (wake-on-work). RecoveryPollSeconds={RecoverySeconds}",
            recovery.TotalSeconds);

        workSignal.NotifyWorkAvailable();

        var waitTimeout = recovery;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await workSignal.WaitAsync(waitTimeout, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            TimeSpan? nextAttemptDelay = null;
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = await DrainOnceAsync(stoppingToken);
                    if (result.HadDueWork)
                    {
                        if (drainIdleDelay > TimeSpan.Zero)
                        {
                            try
                            {
                                await Task.Delay(drainIdleDelay, stoppingToken);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                return;
                            }
                        }

                        continue;
                    }

                    nextAttemptDelay = result.NextAttemptDelay;
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Document PDF outbox drain failed; will retry on next wake.");
                nextAttemptDelay = null;
            }

            waitTimeout = recovery;
            if (nextAttemptDelay is { } delay && delay > TimeSpan.Zero && delay < recovery)
            {
                waitTimeout = delay;
            }
        }
    }

    private async Task<DrainResult> DrainOnceAsync(CancellationToken stoppingToken)
    {
        using var discovery = scopes.CreateScope();
        var targets = await discovery.ServiceProvider.GetRequiredService<DapperExecutor>()
            .QueryAsync<Target>(Net_DB.Net_DB_Manager, "CALL getEInvoicePublicLookupCompanyTargets(NULL)");

        var hadDueWork = false;
        DateTime? earliestNextAttemptUtc = null;

        foreach (var target in targets)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            var previous = accessor.HttpContext;
            try
            {
                var context = new DefaultHttpContext { RequestServices = services };
                context.Items[CompanyRouteContextMiddleware.CompanyCdItemKey] = target.COMPANY_CD;
                accessor.HttpContext = context;
                var db = services.GetRequiredService<DapperExecutor>();
                var pending = (await db.QueryAsync<Pending>(Net_DB.Net_DB_Company, """
                    SELECT DOCUMENT_KIND,DOCUMENT_ID,SOURCE_REVISION FROM electronic_document_pdf_pending
                    WHERE COMPANY_CD=@company AND ATTEMPTS<5 AND NEXT_ATTEMPT_AT<=UTC_TIMESTAMP(6)
                    ORDER BY NEXT_ATTEMPT_AT LIMIT 10
                    """, new { company = target.COMPANY_CD }, sDBName: target.DB_NAME)).ToList();

                if (pending.Count > 0)
                {
                    hadDueWork = true;
                }

                foreach (var item in pending)
                {
                    var args = new
                    {
                        company = target.COMPANY_CD,
                        kind = item.DOCUMENT_KIND,
                        id = item.DOCUMENT_ID,
                        revision = item.SOURCE_REVISION
                    };
                    try
                    {
                        _ = item.DOCUMENT_KIND switch
                        {
                            "invoice" => await services.GetRequiredService<IEInvoicePrintService>()
                                .ExportPdfAsync(target.COMPANY_CD, item.DOCUMENT_ID, cancellationToken: stoppingToken),
                            "minute" => await services.GetRequiredService<IEInvoiceMinutePreviewService>()
                                .ExportPdfAsync(target.COMPANY_CD, item.DOCUMENT_ID, stoppingToken),
                            "declaration" => await services.GetRequiredService<IEInvoiceDeclarationPreviewService>()
                                .ExportPdfAsync(target.COMPANY_CD, item.DOCUMENT_ID, stoppingToken),
                            "error-notice" => await services.GetRequiredService<IEInvoiceErrorNoticePreviewService>()
                                .ExportPdfAsync(target.COMPANY_CD, item.DOCUMENT_ID, stoppingToken),
                            var kind when kind.StartsWith("pit-", StringComparison.Ordinal) =>
                                await services.GetRequiredService<IPitService>()
                                    .PrintPdf(target.COMPANY_CD, kind[4..], item.DOCUMENT_ID, stoppingToken),
                            _ => throw new InvalidOperationException("Unsupported PDF outbox document kind.")
                        };
                        await db.ExecuteAsync(Net_DB.Net_DB_Company, """
                            DELETE FROM electronic_document_pdf_pending WHERE COMPANY_CD=@company AND DOCUMENT_KIND=@kind
                                AND DOCUMENT_ID=@id AND SOURCE_REVISION=@revision
                            """, args, sDBName: target.DB_NAME);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return new DrainResult { HadDueWork = hadDueWork };
                    }
                    catch (Exception ex)
                    {
                        await db.ExecuteAsync(Net_DB.Net_DB_Company, """
                            UPDATE electronic_document_pdf_pending SET ATTEMPTS=ATTEMPTS+1,
                                NEXT_ATTEMPT_AT=TIMESTAMPADD(SECOND,LEAST(900,30*POW(2,ATTEMPTS)),UTC_TIMESTAMP(6))
                            WHERE COMPANY_CD=@company AND DOCUMENT_KIND=@kind AND DOCUMENT_ID=@id AND SOURCE_REVISION=@revision
                            """, args, sDBName: target.DB_NAME);
                        logger.LogWarning(ex, "PDF pending for {Company}/{Kind}/{Id}; signing is not repeated",
                            target.COMPANY_CD, item.DOCUMENT_KIND, item.DOCUMENT_ID);
                    }
                }

                if (pending.Count == 0)
                {
                    var nextAt = await db.QueryFirstOrDefaultAsync<DateTime?>(Net_DB.Net_DB_Company, """
                        SELECT MIN(NEXT_ATTEMPT_AT) FROM electronic_document_pdf_pending
                        WHERE COMPANY_CD=@company AND ATTEMPTS<5 AND NEXT_ATTEMPT_AT>UTC_TIMESTAMP(6)
                        """, new { company = target.COMPANY_CD }, sDBName: target.DB_NAME);
                    if (nextAt is { } at && (earliestNextAttemptUtc is null || at < earliestNextAttemptUtc))
                    {
                        earliestNextAttemptUtc = at;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Document PDF queue unavailable for {Company}", target.COMPANY_CD);
            }
            finally
            {
                accessor.HttpContext = previous;
            }
        }

        TimeSpan? nextDelay = null;
        if (earliestNextAttemptUtc is { } nextUtc)
        {
            // NEXT_ATTEMPT_AT is stored as UTC wall clock without Kind; treat as UTC.
            var utc = DateTime.SpecifyKind(nextUtc, DateTimeKind.Utc);
            var delay = utc - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                nextDelay = delay;
            }
        }

        return new DrainResult
        {
            HadDueWork = hadDueWork,
            NextAttemptDelay = hadDueWork ? null : nextDelay
        };
    }
}
