using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Transmission;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoiceMessageXmlBuilder
    {
        internal const string MessageVersion = MessageEnvelopeBuilder.DefaultVersion;
        internal const string MessageCodePrefix = MessageEnvelopeBuilder.DefaultSenderTaxCode;
        internal const string DefaultSenderTaxCode = MessageEnvelopeBuilder.DefaultSenderTaxCode;
        internal const string DefaultReceiverTaxCode = MessageEnvelopeBuilder.DefaultReceiverTaxCode;
        public static string GenerateMessageCode()
        {
            return MessageEnvelopeBuilder.GenerateMessageCode();
        }
        public static EInvoicePackagedMessage PackageSignedInvoice(string signedInvoiceXml, EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            if (string.IsNullOrWhiteSpace(signedInvoiceXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            var signedRoot = LoadSignedRoot(signedInvoiceXml, "HDon");
            var mtdiep = Common.NormalizeNullableText(invoice.MTDIEP);
            if (!HasText(mtdiep))
                throw EInvoiceValidationMessages.RequiredArgument("MTDIEP");
            var mnGui = Common.NormalizeNullableText(invoice.MSTTCGP) ?? DefaultSenderTaxCode;
            var sellerTaxCode = Common.NormalizeNullableText(seller.SELLER_TAX_CD)
                ?? Common.NormalizeNullableText(invoice.SELLER_TAX_CD);
            return BuildMessage(
                new[] { signedRoot },
                mnGui,
                DefaultReceiverTaxCode,
                EInvoiceMessageTypeCodes.SendInvoice,
                mtdiep!,
                string.Empty,
                sellerTaxCode,
                quantity: 1);
        }

        /// <summary>
        /// QD 1233: MLTDiep 208 (có mã) / 210 (không mã) — DLieu chứa 1 HDon đã ký + 1 BKe đã ký.
        /// </summary>
        public static EInvoicePackagedMessage PackageSignedInvoiceWithBke(
            string signedInvoiceXml,
            string signedBkeXml,
            EInvoiceInfo invoice,
            EInvoiceSellerInfo seller)
        {
            if (string.IsNullOrWhiteSpace(signedInvoiceXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            if (string.IsNullOrWhiteSpace(signedBkeXml))
                throw EInvoiceValidationMessages.RequiredArgument("BKE_XML");

            var signedInvoiceRoot = LoadSignedRoot(signedInvoiceXml, "HDon");
            var signedBkeRoot = LoadSignedRoot(signedBkeXml, "BKe");
            var mtdiep = Common.NormalizeNullableText(invoice.MTDIEP);
            if (!HasText(mtdiep))
                throw EInvoiceValidationMessages.RequiredArgument("MTDIEP");

            var mnGui = Common.NormalizeNullableText(invoice.MSTTCGP) ?? DefaultSenderTaxCode;
            var sellerTaxCode = Common.NormalizeNullableText(seller.SELLER_TAX_CD)
                ?? Common.NormalizeNullableText(invoice.SELLER_TAX_CD);
            var messageType = ResolveMultiInvoiceWithBkeMessageType(invoice);

            return BuildMessage(
                new[] { signedInvoiceRoot, signedBkeRoot },
                mnGui,
                DefaultReceiverTaxCode,
                messageType,
                mtdiep!,
                string.Empty,
                sellerTaxCode,
                quantity: 1);
        }

        public static string ResolveMultiInvoiceWithBkeMessageType(EInvoiceInfo invoice)
        {
            // Ký hiệu HĐ bắt đầu bằng K = không mã → 210; còn lại (thường C) = có mã → 208.
            var series = Common.NormalizeNullableText(invoice.KHHDON) ?? string.Empty;
            if (series.Length > 0 && (series[0] == 'K' || series[0] == 'k'))
            {
                return EInvoiceMessageTypeCodes.SendMultiInvoiceWithoutCode;
            }

            return EInvoiceMessageTypeCodes.SendMultiInvoiceAdjustment;
        }

        public static EInvoicePackagedMessage PackageSignedDeclaration(string signedDeclarationXml, EInvoiceDeclarationInfo declaration, string mtdiep)
        {
            if (string.IsNullOrWhiteSpace(signedDeclarationXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            if (!HasText(mtdiep))
                throw EInvoiceValidationMessages.RequiredArgument("MTDIEP");
            var signedRoot = LoadSignedRoot(signedDeclarationXml, "TKhai");
            var taxpayerTaxCode = Common.NormalizeNullableText(declaration.MST);
            return BuildMessage(
                new[] { signedRoot },
                DefaultSenderTaxCode,
                DefaultReceiverTaxCode,
                EInvoiceMessageTypeCodes.SendDeclaration,
                mtdiep,
                string.Empty,
                taxpayerTaxCode,
                quantity: 1);
        }
        public static EInvoicePackagedMessage PackageSignedErrorNotice(string signedErrorNoticeXml, EInvoiceErrorNoticeInfo notice, string mtdiep)
        {
            if (string.IsNullOrWhiteSpace(signedErrorNoticeXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            if (!HasText(mtdiep))
                throw EInvoiceValidationMessages.RequiredArgument("MTDIEP");
            var signedRoot = LoadSignedRoot(signedErrorNoticeXml, "TBao");
            var taxpayerTaxCode = Common.NormalizeNullableText(notice.MST);
            return BuildMessage(
                new[] { signedRoot },
                DefaultSenderTaxCode,
                DefaultReceiverTaxCode,
                EInvoiceMessageTypeCodes.InvoiceErrorNotice,
                mtdiep,
                string.Empty,
                taxpayerTaxCode,
                quantity: 1);
        }
        private static EInvoicePackagedMessage BuildMessage(
            IReadOnlyList<XElement> payloadRoots,
            string senderTaxCode,
            string receiverTaxCode,
            string messageTypeCode,
            string messageCode,
            string referenceMessageCode,
            string? taxpayerTaxCode,
            int quantity)
        {
            return MessageEnvelopeBuilder.Build(
                payloadRoots,
                messageTypeCode,
                messageCode,
                taxpayerTaxCode,
                version: MessageVersion,
                senderTaxCode: senderTaxCode,
                receiverTaxCode: receiverTaxCode,
                referenceMessageCode: referenceMessageCode,
                quantity: quantity);
        }
        private static XElement LoadSignedRoot(string signedXml, string expectedRootName)
        {
            try
            {
                var document = XDocument.Parse(signedXml, LoadOptions.PreserveWhitespace);
                var root = document.Root
                    ?? throw new InvalidOperationException("Signed XML root element is missing.");
                if (!string.Equals(root.Name.LocalName, expectedRootName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Signed XML root must be {expectedRootName}.");
                }
                return new XElement(root);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException("Signed XML is invalid.", ex);
            }
        }
        private static string Serialize(XDocument document)
        {
            return XmlSerializationHelper.Serialize(document);
        }
        private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
    }
}