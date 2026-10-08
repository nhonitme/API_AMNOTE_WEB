using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Transmission;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitMessageReceiveApplyHandler(DapperExecutor db)
    : IMessageReceiveApplyHandler
{
    public bool CanHandle(string targetType)
        => PitKinds.IsTarget(EInvoiceMessageTargetTypes.Normalize(targetType));

    public async Task ApplyAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received,
        bool preferSuccessResult)
    {
        if (!item.TARGET_ID.HasValue || string.IsNullOrWhiteSpace(item.DB_NAME))
            return;

        var targetType = EInvoiceMessageTargetTypes.Normalize(item.TARGET_TYPE);
        foreach (var message in received)
        {
            var result = MessageReceiveParser.TryParse(
                targetType,
                message.MLTDIEP,
                message.DLieu);
            if (result?.Status == null)
                continue;

            await db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                "CALL setPitReceive(@company,@target,@id,@message,@status,@priority,@xml,@error)",
                new
                {
                    company = item.COMPANY_CD,
                    target = targetType,
                    id = item.TARGET_ID.Value,
                    message = item.SEND_MTDIEP,
                    status = result.Status.Value,
                    priority = result.Priority,
                    xml = message.DLieu,
                    error = result.Error
                },
                sDBName: item.DB_NAME);
        }
    }
}
