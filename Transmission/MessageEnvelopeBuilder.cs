using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Transmission;

public static class MessageEnvelopeBuilder
{
    public const string DefaultVersion = "2.1.0";
    public const string DefaultSenderTaxCode = "0312270160";
    public const string DefaultReceiverTaxCode = "0100109106";

    public static string GenerateMessageCode()
        => DefaultSenderTaxCode + Guid.NewGuid().ToString("N").ToUpperInvariant();

    public static EInvoicePackagedMessage Build(
        IReadOnlyList<XElement> payloadRoots,
        string messageTypeCode,
        string messageCode,
        string? taxpayerTaxCode,
        string version = DefaultVersion,
        string senderTaxCode = DefaultSenderTaxCode,
        string receiverTaxCode = DefaultReceiverTaxCode,
        string referenceMessageCode = "",
        int quantity = 1,
        string? dataId = null,
        bool includeEmptyTaxpayerSignature = false,
        bool includeXmlDeclaration = true)
    {
        if (payloadRoots.Count == 0)
            throw new InvalidOperationException("Message payload is required.");

        var data = new XElement("DLieu", payloadRoots.Select(root => new XElement(root)));
        if (!string.IsNullOrWhiteSpace(dataId))
            data.SetAttributeValue("Id", dataId);

        var root = new XElement(
            "TDiep",
                new XElement(
                    "TTChung",
                    new XElement("PBan", version),
                    new XElement("MNGui", senderTaxCode),
                    new XElement("MNNhan", receiverTaxCode),
                    new XElement("MLTDiep", messageTypeCode),
                    new XElement("MTDiep", messageCode),
                    new XElement("MTDTChieu", referenceMessageCode),
                    new XElement("MST", taxpayerTaxCode ?? string.Empty),
                    new XElement("SLuong", quantity)),
                data);
        if (includeEmptyTaxpayerSignature)
            root.Add(new XElement("CKSNNT"));

        var document = new XDocument(root);

        var requestXml = includeXmlDeclaration
            ? XmlSerializationHelper.Serialize(document)
            : root.ToString(SaveOptions.DisableFormatting);

        return new EInvoicePackagedMessage(
            requestXml,
            version,
            senderTaxCode,
            receiverTaxCode,
            messageTypeCode,
            messageCode,
            referenceMessageCode,
            taxpayerTaxCode ?? string.Empty,
            quantity);
    }
}
