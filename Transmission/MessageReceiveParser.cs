using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.TaxWithholding;
using System.Xml;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Transmission;

public sealed record MessageReceiveOutcome(
    int? Status,
    int Priority,
    string? Error,
    bool Terminal,
    string? PayloadXml = null,
    string? Mccqt = null);

public sealed record MttReceiveOutcome(bool Accepted, string? Error);

public static class MessageReceiveParser
{
    public static MessageReceiveOutcome? TryParse(string? targetType, string? receiveTypeCode, string? receiveXml)
    {
        var target = EInvoiceMessageTargetTypes.Normalize(targetType);
        var code = (receiveTypeCode ?? string.Empty).Trim();
        var xml = receiveXml ?? string.Empty;

        var transmissionError = MessageReceiveXmlParser.TryExtractTransmissionErrorMessage(xml);
        if (!string.IsNullOrWhiteSpace(transmissionError))
        {
            return Rejected(target, transmissionError, 30);
        }
        if (code is EInvoiceMessageTypeCodes.InvalidFormatResponse or EInvoiceMessageTypeCodes.InvalidSigningDataResponse)
            return Rejected(
                target,
                MessageReceiveXmlParser.TryExtractDirectErrorMessage(xml)
                    ?? "Thông điệp không đúng định dạng hoặc dữ liệu ký không hợp lệ.",
                30);

        if (PitKinds.IsTarget(target))
            return TryParsePit(target, code, xml);
        if (EInvoiceMessageTargetTypes.IsDeclaration(target))
            return TryParse100Series(code, xml);
        if (EInvoiceMessageTargetTypes.IsErrorNotice(target))
            return TryParse300Series(code, xml);
        return TryParse200Series(code, xml);
    }

    public static bool IsTerminal(string? targetType, string? receiveTypeCode, string? receiveXml)
        => TryParse(targetType, receiveTypeCode, receiveXml)?.Terminal == true;

    public static MessageReceiveOutcome? TryParse100Series(string code, string xml)
    {
        if (EInvoiceMessageTypeCodes.IsDeclarationAcceptNotice(code))
        {
            var authorityStatus = MessageReceiveXmlParser.TryExtractTtxncqt(xml);
            var status = authorityStatus switch
            {
                1 => EInvoiceDeclarationCqtStatus.Accepted,
                2 => EInvoiceDeclarationCqtStatus.Rejected,
                _ => EInvoiceDeclarationCqtStatus.SentWaiting
            };
            var error = status == EInvoiceDeclarationCqtStatus.Rejected
                ? MessageReceiveXmlParser.TryExtractDeclarationErrorMessage(xml)
                : null;
            var payload = MessageReceiveXmlParser.TryExtractDeclarationAcceptNoticeXml(xml)
                ?? MessageReceiveXmlParser.TryExtractDeclarationXml(xml);
            return new(status, 20, error, true, payload, MessageReceiveXmlParser.TryExtractMccqt(payload));
        }

        if (EInvoiceMessageTypeCodes.IsDeclarationReceiveNotice(code))
        {
            var rejected = MessageReceiveXmlParser.IsDeclarationRejection(xml);
            return new(
                rejected ? EInvoiceDeclarationCqtStatus.Rejected : EInvoiceDeclarationCqtStatus.SentWaiting,
                10,
                rejected ? MessageReceiveXmlParser.TryExtractDeclarationErrorMessage(xml) : null,
                rejected);
        }

        return null;
    }

    public static MessageReceiveOutcome? TryParse200Series(string code, string xml)
    {
        if (EInvoiceMessageTypeCodes.IsInvoiceTaxCodeResult(code))
        {
            return new(
                null,
                20,
                null,
                true,
                MessageReceiveXmlParser.TryExtractInvoiceXml(xml) ?? xml.Trim(),
                MessageReceiveXmlParser.TryExtractMccqt(xml));
        }

        if (EInvoiceMessageTypeCodes.IsDataCheckResultNotice01(code))
        {
            var error = MessageReceiveXmlParser.TryExtractDataCheckErrorMessage(xml);
            return new(
                string.IsNullOrWhiteSpace(error) ? null : EInvoiceInvoiceStatusCodes.CodeError,
                20,
                error,
                true);
        }

        return null;
    }

    public static MessageReceiveOutcome? TryParse300Series(string code, string xml)
    {
        if (!EInvoiceMessageTypeCodes.IsInvoiceErrorProcessResult(code))
            return null;

        var rejected = MessageReceiveXmlParser.IsErrorNoticeProcessRejection(xml);
        return new(
            rejected ? EInvoiceDeclarationCqtStatus.Rejected : EInvoiceDeclarationCqtStatus.Accepted,
            20,
            rejected ? MessageReceiveXmlParser.TryExtractErrorNoticeProcessMessage(xml) : null,
            true,
            rejected ? null : MessageReceiveXmlParser.TryExtractErrorNoticeXml(xml));
    }

    public static MessageReceiveOutcome? TryParsePit(string target, string code, string xml)
    {
        if (!TryLoadDocument(xml, out var root)) return null;
        string? Get(string name) => root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim();

        int? status = null;
        var priority = 0;
        var terminal = true;

        if (target == PitKinds.Target(PitKinds.Declaration) && code == "111")
        {
            status = Get("TTXNCQT") switch { "1" => 2, "2" => 3, _ => null };
            priority = 20;
        }
        else if (target == PitKinds.Target(PitKinds.Declaration) && code == "110")
        {
            status = Get("THop") switch { "1" or "3" => 1, "2" or "4" => 3, _ => null };
            priority = 10;
            terminal = status == 3;
        }
        else if (target == PitKinds.Target(PitKinds.Certificate) && code == "213")
        {
            status = Get("LTBao") switch { "2" => 2, "10" or "11" or "12" or "13" => 3, _ => null };
            priority = 20;
        }
        else if (target == PitKinds.Target(PitKinds.ErrorNotice) && code == "301")
        {
            status = MessageReceiveXmlParser.IsErrorNoticeProcessRejection(xml) ? 3 : 2;
            priority = 20;
        }

        if (status == null) return null;

        var error = status == 3
            ? MessageReceiveXmlParser.TryExtractAnyErrorMessage(xml)
            : null;

        return new(
            status.Value,
            priority,
            status == 3 && string.IsNullOrWhiteSpace(error) ? "CQT không chấp nhận dữ liệu." : error,
            terminal);
    }

    private static MessageReceiveOutcome Rejected(string target,string error,int priority)
    {
        var status=string.Equals(target,EInvoiceMessageTargetTypes.Invoice,StringComparison.Ordinal)
            ? EInvoiceInvoiceStatusCodes.CodeError
            : EInvoiceDeclarationCqtStatus.Rejected;
        return new(status,priority,error,true);
    }

    public static MttReceiveOutcome? TryParseMtt(string? xml)
    {
        var transmissionError = MessageReceiveXmlParser.TryExtractTransmissionErrorMessage(xml);
        if (!string.IsNullOrWhiteSpace(transmissionError))
            return new(false, transmissionError);

        if (!TryLoadDocument(xml, out var document))
            return null;
        var notice = document.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "LTBao")
            ?.Value.Trim();
        if (notice is not ("2" or "7" or "9"))
            return null;

        var reasons = document.Descendants()
            .Where(e => e.Name.LocalName == "LDo")
            .Select(e => string.Join(
                " - ",
                e.Elements()
                    .Where(c => c.Name.LocalName is "MLoi" or "MTLoi" or "HDXLy")
                    .Select(c => c.Value.Trim())))
            .Where(reason => reason.Length > 0);
        var error = notice == "2" ? null : string.Join("; ", reasons);

        if (notice != "2" && string.IsNullOrWhiteSpace(error))
            error = $"CQT từ chối gói dữ liệu MTT (LTBao={notice}).";

        return new(notice == "2", error);
    }

    private static bool TryLoadDocument(string? xml, out XDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(xml))
            return false;

        try
        {
            document = XDocument.Parse(xml);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
