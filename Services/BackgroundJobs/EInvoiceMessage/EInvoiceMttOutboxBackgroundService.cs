using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.EInvoiceMessage;

// Recover a signed company transaction if the process stopped before manager queue insertion.
public sealed class EInvoiceMttOutboxBackgroundService(IServiceScopeFactory scopes, IOptions<EInvoiceGatewayOptions> options,
    ILogger<EInvoiceMttOutboxBackgroundService> logger) : BackgroundService
{
    private sealed class Target { public string COMPANY_CD { get; set; } = ""; public string DB_NAME { get; set; } = ""; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DapperExecutor>();
                var repository = scope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();
                var targets = await db.QueryAsync<Target>(Net_DB.Net_DB_Manager, "CALL getEInvoicePublicLookupCompanyTargets(NULL)");
                foreach (var target in targets)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    try
                    {
                        await repository.DispatchMttOutboxAsync(target.DB_NAME, target.COMPANY_CD);
                    }
                    catch (Exception ex) { logger.LogWarning(ex,"MTT outbox retry pending for {Company}",target.COMPANY_CD); }
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<API_AMNOTE_WEB.TaxWithholding.PitMessageOutboundService>()
                            .DispatchAsync(target.DB_NAME, target.COMPANY_CD);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "PIT outbox retry pending for {Company}", target.COMPANY_CD);
                    }
                }
            }
            catch (Exception ex) { logger.LogWarning(ex,"MTT outbox recovery failed; retry in 60 seconds."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
