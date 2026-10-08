using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Transmission;

public sealed class EInvoiceMessageReceiveApplyHandler(
    IEInvoiceMessageRepository messages,
    IEInvoiceRepository invoices,
    IOptions<EInvoiceGatewayOptions> options) : IMessageReceiveApplyHandler
{
    private readonly string _systemUserId = options.Value.SystemUserId;

    public bool CanHandle(string targetType)
        => !API_AMNOTE_WEB.TaxWithholding.PitKinds.IsTarget(
            EInvoiceMessageTargetTypes.Normalize(targetType));

    public async Task ApplyAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received,
        bool preferSuccessResult)
    {
        if (!item.TARGET_ID.HasValue || string.IsNullOrWhiteSpace(item.DB_NAME))
            return;

        var target = EInvoiceMessageTargetTypes.Normalize(item.TARGET_TYPE);
        if (EInvoiceMessageTargetTypes.IsDeclaration(target))
        {
            await ApplyDeclarationAsync(item, received, preferSuccessResult);
            return;
        }

        if (EInvoiceMessageTargetTypes.IsErrorNotice(target))
        {
            await ApplyErrorNoticeAsync(item, received);
            return;
        }

        await ApplyInvoiceAsync(item, received, preferSuccessResult);
    }

    private async Task ApplyInvoiceAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received,
        bool preferTaxCodeResult)
    {
        var sent = await messages.GetSendMessageByMtdiepAsync(item.COMPANY_CD, item.SEND_MTDIEP);
        if (sent?.MLTDIEP is "206" or "216")
        {
            foreach (var message in received.Where(x =>
                         EInvoiceMessageTypeCodes.IsDataCheckResultNotice01(x.MLTDIEP)
                         || !string.IsNullOrWhiteSpace(
                             MessageReceiveXmlParser.TryExtractTransmissionErrorMessage(x.DLieu))))
            {
                await messages.ApplyMttReceiveAsync(
                    item.DB_NAME,
                    item.COMPANY_CD,
                    _systemUserId,
                    item.SEND_MTDIEP,
                    message.DLieu);
            }
            return;
        }

        var candidates = received
            .Select(message => (Message: message, Outcome: MessageReceiveParser.TryParse(
                EInvoiceMessageTargetTypes.Invoice,
                message.MLTDIEP,
                message.DLieu)))
            .Where(x => x.Outcome != null)
            .OrderByDescending(x => ReferencesSentMessage(x.Message, item.SEND_MTDIEP))
            .ToList();

        if (preferTaxCodeResult)
        {
            var accepted = candidates
                .Where(x => EInvoiceMessageTypeCodes.IsInvoiceTaxCodeResult(x.Message.MLTDIEP))
                .ToList();
            foreach (var candidate in accepted)
            {
                var outcome = candidate.Outcome!;
                var invoiceXml = outcome.PayloadXml ?? candidate.Message.DLieu;
                var fallbackTchdon = await invoices.GetTchdonAsync(
                    item.COMPANY_CD,
                    item.TARGET_ID!.Value,
                    item.DB_NAME);
                var invoiceStatus = ResolveInvoiceStatus(candidate.Message.DLieu, fallbackTchdon);

                await messages.ApplyReceiveXmlToInvoiceAsync(
                    item.DB_NAME,
                    item.COMPANY_CD,
                    _systemUserId,
                    item.TARGET_ID.Value,
                    invoiceXml,
                    outcome.Mccqt,
                    invoiceStatus);
            }
            if (accepted.Count > 0)
                return;
        }

        var rejected = candidates.FirstOrDefault(x =>
            EInvoiceMessageTypeCodes.IsDataCheckResultNotice01(x.Message.MLTDIEP)
            || x.Outcome!.Status == EInvoiceInvoiceStatusCodes.CodeError
            || !string.IsNullOrWhiteSpace(x.Outcome.Error));
        if (rejected.Outcome == null)
            return;

        await messages.ApplyReceiveErrorToInvoiceAsync(
            item.DB_NAME,
            item.COMPANY_CD,
            _systemUserId,
            item.TARGET_ID.Value,
            rejected.Outcome.Error ?? "CQT không chấp nhận dữ liệu hóa đơn.",
            EInvoiceInvoiceStatusCodes.CodeError);
    }

    private async Task ApplyDeclarationAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received,
        bool preferAcceptNotice)
    {
        var candidates = received
            .Select(message => (Message: message, Outcome: MessageReceiveParser.TryParse(
                EInvoiceMessageTargetTypes.Declaration,
                message.MLTDIEP,
                message.DLieu)))
            .Where(x => x.Outcome != null)
            .OrderByDescending(x => ReferencesSentMessage(x.Message, item.SEND_MTDIEP))
            .ToList();

        if (preferAcceptNotice)
        {
            var accepted = candidates.FirstOrDefault(x =>
                EInvoiceMessageTypeCodes.IsDeclarationAcceptNotice(x.Message.MLTDIEP)
                && !string.IsNullOrWhiteSpace(x.Outcome!.PayloadXml));
            if (accepted.Outcome != null)
            {
                await messages.ApplyReceiveXmlToDeclarationAsync(
                    item.DB_NAME,
                    item.COMPANY_CD,
                    _systemUserId,
                    item.TARGET_ID.Value,
                    accepted.Outcome.PayloadXml!,
                    accepted.Outcome.Status ?? EInvoiceDeclarationCqtStatus.SentWaiting,
                    accepted.Outcome.Mccqt);

                if (accepted.Outcome.Status == EInvoiceDeclarationCqtStatus.Rejected)
                {
                    await messages.ApplyReceiveErrorToDeclarationAsync(
                        item.DB_NAME,
                        item.COMPANY_CD,
                        _systemUserId,
                        item.TARGET_ID.Value,
                        accepted.Outcome.Error ?? "CQT không chấp nhận tờ khai.",
                        EInvoiceDeclarationCqtStatus.Rejected);
                }
                return;
            }
        }

        var rejected = candidates.FirstOrDefault(x =>
            x.Outcome!.Terminal
            && (EInvoiceMessageTypeCodes.IsDeclarationReceiveNotice(x.Message.MLTDIEP)
                || x.Outcome.Status == EInvoiceDeclarationCqtStatus.Rejected
                || !string.IsNullOrWhiteSpace(x.Outcome.Error)));
        if (rejected.Outcome == null)
            return;

        await messages.ApplyReceiveErrorToDeclarationAsync(
            item.DB_NAME,
            item.COMPANY_CD,
            _systemUserId,
            item.TARGET_ID!.Value,
            rejected.Outcome.Error ?? "CQT không tiếp nhận tờ khai.",
            EInvoiceDeclarationCqtStatus.Rejected);
    }

    private async Task ApplyErrorNoticeAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received)
    {
        var result = received
            .Select(message => (Message: message, Outcome: MessageReceiveParser.TryParse(
                EInvoiceMessageTargetTypes.ErrorNotice,
                message.MLTDIEP,
                message.DLieu)))
            .Where(x => x.Outcome != null)
            .OrderByDescending(x => ReferencesSentMessage(x.Message, item.SEND_MTDIEP))
            .FirstOrDefault();
        if (result.Outcome == null)
            return;

        if (result.Outcome.Status == EInvoiceDeclarationCqtStatus.Rejected)
        {
            await messages.ApplyReceiveErrorToErrorNoticeAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _systemUserId,
                item.TARGET_ID!.Value,
                result.Outcome.Error ?? "CQT không chấp nhận thông báo sai sót.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Outcome.PayloadXml))
        {
            await messages.ApplyReceiveXmlToErrorNoticeAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _systemUserId,
                item.TARGET_ID!.Value,
                result.Outcome.PayloadXml);
        }
        else
        {
            await messages.ApplyReceiveErrorToErrorNoticeAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _systemUserId,
                item.TARGET_ID!.Value,
                null);
        }
    }

    private static int ResolveInvoiceStatus(string? xml, int? fallbackTchdon)
    {
        if (!MessageReceiveXmlParser.HasTthdlQuan(xml))
            return EInvoiceInvoiceStatusCodes.Original;

        return (MessageReceiveXmlParser.TryExtractRelatedTchdon(xml) ?? fallbackTchdon ?? 0) switch
        {
            1 => EInvoiceInvoiceStatusCodes.Replacement,
            2 => EInvoiceInvoiceStatusCodes.Adjustment,
            _ => EInvoiceInvoiceStatusCodes.Original
        };
    }

    private static bool ReferencesSentMessage(
        ViettelEInvoiceMessageLookupResult message,
        string sentMessageCode)
    {
        var reference = string.IsNullOrWhiteSpace(message.MTDTCHIEU)
            ? MessageReceiveXmlParser.TryExtractMtdtchieu(message.DLieu)
            : message.MTDTCHIEU.Trim();
        return string.Equals(reference, sentMessageCode, StringComparison.Ordinal);
    }
}
