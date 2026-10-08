using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Transmission;

internal static class GatewayMessageMatcher
{
    public const string GatewaySenderTaxCode = "V0100109106";

    public static bool IsGatewaySender(string? sender)
    {
        var normalized = (sender ?? string.Empty).Trim();
        return normalized.Length > 0
            && (string.Equals(normalized, GatewaySenderTaxCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    normalized,
                    "V" + MessageEnvelopeBuilder.DefaultReceiverTaxCode,
                    StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSendPayloadMessageType(string? messageType)
    {
        var code = (messageType ?? string.Empty).Trim();
        return code is
            EInvoiceMessageTypeCodes.SendDeclaration
            or EInvoiceMessageTypeCodes.SendDeclarationDelegation
            or EInvoiceMessageTypeCodes.SendElectronicDocumentDeclaration
            or EInvoiceMessageTypeCodes.SendInvoice
            or EInvoiceMessageTypeCodes.SendInvoicePerOccurrence
            or EInvoiceMessageTypeCodes.SendInvoiceWithoutCode
            or EInvoiceMessageTypeCodes.SendCashRegisterInvoice
            or EInvoiceMessageTypeCodes.SendCashRegisterInvoiceWithoutCode
            or EInvoiceMessageTypeCodes.SendMultiInvoiceAdjustment
            or EInvoiceMessageTypeCodes.SendMultiInvoiceWithoutCode
            or EInvoiceMessageTypeCodes.SendPersonalIncomeTaxCertificate
            or EInvoiceMessageTypeCodes.InvoiceErrorNotice
            or EInvoiceMessageTypeCodes.ElectronicDocumentErrorNotice;
    }

    public static bool ShouldAssignTransactionCode(ViettelEInvoiceMessageLookupResult lookup)
        => IsGatewaySender(lookup.MNGUI)
            && IsSendPayloadMessageType(lookup.MLTDIEP)
            && !string.IsNullOrWhiteSpace(lookup.MTDIEP);

    public static string ResolveReference(
        ViettelEInvoiceMessageLookupResult message,
        string fallbackMessageCode)
    {
        if (!string.IsNullOrWhiteSpace(message.MTDTCHIEU))
            return message.MTDTCHIEU.Trim();

        return MessageReceiveXmlParser.TryExtractMtdtchieu(message.DLieu)
            ?? fallbackMessageCode;
    }

    public static ViettelEInvoiceMessageLookupResult? SelectSendEcho(
        IEnumerable<ViettelEInvoiceMessageLookupResult> messages,
        string sentMessageCode)
    {
        var normalized = (sentMessageCode ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return null;

        return messages
            .Where(ShouldAssignTransactionCode)
            .OrderByDescending(message => ReferencesSentMessage(message, normalized))
            .ThenByDescending(message =>
                string.Equals(message.MTDIEP?.Trim(), normalized, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
    }

    private static bool ReferencesSentMessage(
        ViettelEInvoiceMessageLookupResult message,
        string sentMessageCode)
    {
        var reference = ResolveReference(message, message.MTDIEP ?? string.Empty);
        return string.Equals(reference, sentMessageCode, StringComparison.OrdinalIgnoreCase);
    }
}
