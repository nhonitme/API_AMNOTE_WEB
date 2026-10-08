using API_AMNOTE_WEB.Helpers;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Transmission;

public static class MessageReceiveXmlParser
{
    public static string? TryExtractInvoiceXml(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.Name.LocalName, "HDon", StringComparison.OrdinalIgnoreCase))
            {
                return SerializeElement(root);
            }

            var hdon = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "HDon", StringComparison.OrdinalIgnoreCase));

            return hdon == null ? dlieu.Trim() : SerializeElement(hdon);
        }
        catch (XmlException)
        {
            return dlieu.Trim();
        }
    }

    public static string? TryExtractMtdtchieu(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var mtdtchieu = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "MTDTChieu", StringComparison.OrdinalIgnoreCase));

            var value = mtdtchieu?.Value?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static string? TryExtractMccqt(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var mccqtElement = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "MCCQT", StringComparison.OrdinalIgnoreCase));

            var value = mccqtElement?.Value?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static bool HasTthdlQuan(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            return document
                .Descendants()
                .Any(element => string.Equals(element.Name.LocalName, "TTHDLQuan", StringComparison.OrdinalIgnoreCase));
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static int? TryExtractRelatedTchdon(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var relatedElement = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TTHDLQuan", StringComparison.OrdinalIgnoreCase));

            if (relatedElement == null)
            {
                return null;
            }

            var tchdonText = GetChildValue(relatedElement, "TCHDon")
                ?? GetChildValue(relatedElement, "TCHDON");

            if (string.IsNullOrWhiteSpace(tchdonText) || !int.TryParse(tchdonText.Trim(), out var tchdon))
            {
                return null;
            }

            return tchdon;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static string? TryExtractErrorNoticeXml(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.Name.LocalName, "TBao", StringComparison.OrdinalIgnoreCase))
            {
                return SerializeElement(root);
            }

            var tbao = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TBao", StringComparison.OrdinalIgnoreCase));

            return tbao == null ? null : SerializeElement(tbao);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static bool IsErrorNoticeProcessRejection(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            return IsDeclarationRejection(document)
                || document
                    .Descendants()
                    .Any(element =>
                        string.Equals(element.Name.LocalName, "LDo", StringComparison.OrdinalIgnoreCase)
                        && element.Parent != null
                        && string.Equals(
                            element.Parent.Name.LocalName,
                            "DSLDKTNhan",
                            StringComparison.OrdinalIgnoreCase)
                        && element.Elements().Any(child =>
                            string.Equals(child.Name.LocalName, "MLoi", StringComparison.OrdinalIgnoreCase)));
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Thông điệp 301: gom lỗi tổng và lỗi từng hóa đơn/chứng từ trong DSLDKTNhan/LDo.
    /// </summary>
    public static string? TryExtractErrorNoticeProcessMessage(string? dlieu)
        => TryExtractAnyErrorMessage(dlieu);

    public static string? TryExtractDeclarationXml(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.Name.LocalName, "TKhai", StringComparison.OrdinalIgnoreCase))
            {
                return SerializeElement(root);
            }

            var tkhai = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TKhai", StringComparison.OrdinalIgnoreCase));

            return tkhai == null ? null : SerializeElement(tkhai);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static string? TryExtractDeclarationAcceptNoticeXml(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.Name.LocalName, "DLTBao", StringComparison.OrdinalIgnoreCase))
            {
                return SerializeElement(root);
            }

            if (string.Equals(root.Name.LocalName, "TBao", StringComparison.OrdinalIgnoreCase))
            {
                return SerializeElement(root);
            }

            var dltbao = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "DLTBao", StringComparison.OrdinalIgnoreCase));

            if (dltbao != null)
            {
                return SerializeElement(dltbao);
            }

            var tbao = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TBao", StringComparison.OrdinalIgnoreCase));

            return tbao == null ? null : SerializeElement(tbao);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static int? TryExtractTtxncqt(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var ttxncqtElement = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TTXNCQT", StringComparison.OrdinalIgnoreCase));

            if (ttxncqtElement == null || !int.TryParse(NormalizeText(ttxncqtElement.Value), out var ttxncqt))
            {
                return null;
            }

            return ttxncqt is 1 or 2 ? ttxncqt : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Phản hồi kỹ thuật TDiep/DLieu/TBao: TTTNhan=1 và lỗi nằm trong DSLDo/LDo.
    /// Áp dụng chung cho tờ khai, thông báo sai sót, hóa đơn và chứng từ điện tử.
    /// </summary>
    public static string? TryExtractTransmissionErrorMessage(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var receiveStatus = document
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "TTTNhan", StringComparison.OrdinalIgnoreCase));

            if (!string.Equals(NormalizeText(receiveStatus?.Value), "1", StringComparison.Ordinal))
            {
                return null;
            }

            return BuildReasonMessage(FindStructuredReasonElements(document), includeInvoiceFields: false)
                ?? "Thông điệp không được tiếp nhận.";
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static string? TryExtractAnyErrorMessage(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            return BuildReasonMessage(FindStructuredReasonElements(document), includeInvoiceFields: false);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static string? TryExtractDirectErrorMessage(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu)) return null;
        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var container = document.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TBao", StringComparison.OrdinalIgnoreCase));
            if (container == null) return null;
            var fields = new[] { "MLoi", "MTa", "Mta", "MTLoi", "HDXLy", "GChu" }
                .Select(name => NormalizeText(GetChildValue(container, name)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal);
            var message = NormalizeText(string.Join(" ", fields));
            return message is { Length: > 1000 } ? message[..1000] : message;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Thông điệp 204 / TBao hóa đơn: LTBao=1 — gom MLoi, MTLoi, HDXLy từ DSLDo/LDo.
    /// </summary>
    public static string? TryExtractDataCheckErrorMessage(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            var ltbao = document
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "LTBao", StringComparison.OrdinalIgnoreCase));

            if (ltbao == null || NormalizeText(ltbao.Value) is not ("1" or "3" or "4" or "7" or "9"))
            {
                return null;
            }

            var ldoElements = FindStructuredReasonElements(document);
            if (ldoElements.Count == 0)
            {
                return null;
            }

            return BuildReasonMessage(ldoElements, includeInvoiceFields: true);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Thông điệp 102/103 tờ khai: THop=2 hoặc 4 — gom MLoi, MTa từ DSLDKCNhan/LDo (hoặc DMXLy/LDo).
    /// </summary>
    public static string? TryExtractDeclarationErrorMessage(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            if (!IsDeclarationRejection(document))
            {
                return null;
            }

            var ldoElements = FindDeclarationRejectionReasonElements(document);
            if (ldoElements.Count == 0)
            {
                return null;
            }

            return BuildReasonMessage(ldoElements, includeInvoiceFields: false);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static bool IsDeclarationRejection(string? dlieu)
    {
        if (string.IsNullOrWhiteSpace(dlieu))
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(dlieu, LoadOptions.PreserveWhitespace);
            return IsDeclarationRejection(document);
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool IsDeclarationRejection(XDocument document)
    {
        var thop = document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "THop", StringComparison.OrdinalIgnoreCase));

        var thopValue = NormalizeText(thop?.Value);
        return thopValue is "2" or "4";
    }

    private static List<XElement> FindDeclarationRejectionReasonElements(XDocument document)
    {
        var fromReceiveReject = document
            .Descendants()
            .Where(element =>
                (string.Equals(element.Name.LocalName, "LDo", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(element.Name.LocalName, "LDTTChung", StringComparison.OrdinalIgnoreCase))
                && element.Parent != null
                && string.Equals(element.Parent.Name.LocalName, "DSLDKCNhan", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (fromReceiveReject.Count > 0)
        {
            return fromReceiveReject;
        }

        var fromProcessReject = document
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "LDo", StringComparison.OrdinalIgnoreCase)
                && element.Parent != null
                && string.Equals(element.Parent.Name.LocalName, "DMXLy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (fromProcessReject.Count > 0)
        {
            return fromProcessReject;
        }

        return FindStructuredReasonElements(document);
    }

    private static string? BuildReasonMessage(IReadOnlyList<XElement> ldoElements, bool includeInvoiceFields)
    {
        var parts = new List<string>();
        foreach (var ldo in ldoElements)
        {
            var fields = includeInvoiceFields
                ? new[]
                {
                    NormalizeText(GetChildValue(ldo, "MLoi")),
                    NormalizeText(GetChildValue(ldo, "MTa")),
                    NormalizeText(GetChildValue(ldo, "MTLoi")),
                    NormalizeText(GetChildValue(ldo, "HDXLy")),
                    NormalizeText(GetChildValue(ldo, "GChu"))
                }
                : new[]
                {
                    NormalizeText(GetChildValue(ldo, "MLoi")),
                    NormalizeText(GetChildValue(ldo, "MTa")),
                    NormalizeText(GetChildValue(ldo, "MTLoi")),
                    NormalizeText(GetChildValue(ldo, "HDXLy")),
                    NormalizeText(GetChildValue(ldo, "GChu"))
                };

            var line = string.Join(" ", fields.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(line))
            {
                parts.Add(line);
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        var message = NormalizeText(string.Join("; ", parts.Distinct(StringComparer.Ordinal)));
        return message.Length > 1000 ? message[..1000] : message;
    }

    private static List<XElement> FindStructuredReasonElements(XDocument document)
    {
        return document
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "LDo", StringComparison.OrdinalIgnoreCase)
                && element.Elements().Any(child =>
                    string.Equals(child.Name.LocalName, "MLoi", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name.LocalName, "MTa", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name.LocalName, "MTLoi", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name.LocalName, "HDXLy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name.LocalName, "GChu", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<XElement> FindErrorReasonElements(XDocument document)
    {
        var lcmaBlocks = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "LCMa", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lcmaBlocks.Count > 0)
        {
            var fromLcma = lcmaBlocks
                .SelectMany(lcma => lcma
                    .Descendants()
                    .Where(element =>
                        string.Equals(element.Name.LocalName, "LDo", StringComparison.OrdinalIgnoreCase)
                        && element.Parent != null
                        && string.Equals(element.Parent.Name.LocalName, "DSLDo", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (fromLcma.Count > 0)
            {
                return fromLcma;
            }
        }

        return document
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "LDo", StringComparison.OrdinalIgnoreCase)
                && element.Parent != null
                && string.Equals(element.Parent.Name.LocalName, "DSLDo", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(
            " ",
            value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split(['\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? GetChildValue(XElement parent, string localName)
    {
        return parent
            .Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
    }

    private static string SerializeElement(XElement element)
    {
        return XmlSerializationHelper.Serialize(element);
    }
}
