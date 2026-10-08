using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Transmission;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceTransmissionPreviewService : IEInvoiceTransmissionPreviewService
    {
        private readonly IEInvoiceMessageRepository _messageRepository;
        private readonly IEInvoiceRepository _invoiceRepository;
        private readonly IEInvoiceSellerRepository _sellerRepository;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly IWebHostEnvironment _environment;

        public EInvoiceTransmissionPreviewService(
            IEInvoiceMessageRepository messageRepository,
            IEInvoiceRepository invoiceRepository,
            IEInvoiceSellerRepository sellerRepository,
            IEInvoiceTemplateRepository templateRepository,
            IWebHostEnvironment environment)
        {
            _messageRepository = messageRepository;
            _invoiceRepository = invoiceRepository;
            _sellerRepository = sellerRepository;
            _templateRepository = templateRepository;
            _environment = environment;
        }

        public async Task<string> BuildReceivePreviewHtmlAsync(string companyCd, long receiveId, long? invoiceId = null)
        {
            EInvoiceXslPreviewHelper.ValidateCompany(companyCd);
            if (receiveId <= 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("RECEIVE_ID");
            }

            var message = await _messageRepository.GetReceiveMessageByIdAsync(companyCd, receiveId)
                ?? throw new KeyNotFoundException("E-invoice receive message not found");

            var xml = Common.NormalizeNullableText(message.RESPONSE_XML);
            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new InvalidOperationException("RESPONSE_XML is empty.");
            }

            EInvoiceXslPreviewHelper.ValidateXml(xml, "RESPONSE_XML");

            if (EInvoiceMessageTypeCodes.ShouldPreviewWithSellerXsl(message.MLTDIEP))
            {
                var resolvedInvoiceId = await ResolveInvoiceIdForSellerPreviewAsync(companyCd, message, invoiceId);
                var invoiceXml = ResolveInvoiceXmlForSellerPreview(xml, message.MLTDIEP);
                return await TransformInvoiceXmlWithSellerXslAsync(companyCd, resolvedInvoiceId, invoiceXml);
            }

            var templateCd = Common.NormalizeNullableText(message.MLTDIEP);
            if (string.IsNullOrWhiteSpace(templateCd))
            {
                throw new InvalidOperationException("MLTDIEP is empty.");
            }

            var template = await _templateRepository.GetActiveTemplateAsync(
                EInvoiceXslPreviewHelper.TemplateTypeXsl,
                templateCd)
                ?? throw new InvalidOperationException($"E-invoice message XSL template '{templateCd}' is not configured.");

            var xsl = Common.NormalizeNullableText(template.CONTENT);
            if (string.IsNullOrWhiteSpace(xsl))
            {
                throw new InvalidOperationException($"E-invoice message XSL template '{templateCd}' is empty.");
            }

            return EInvoiceXmlHtmlTransformService.Transform(
                xml,
                xsl,
                lookupCompanyCd: companyCd);
        }

        private static string ResolveInvoiceXmlForSellerPreview(string? xml, string? messageTypeCode)
        {
            var normalizedXml = Common.NormalizeNullableText(xml);
            if (string.IsNullOrWhiteSpace(normalizedXml))
            {
                throw new InvalidOperationException("Message XML is empty.");
            }

            var invoiceXml = MessageReceiveXmlParser.TryExtractInvoiceXml(normalizedXml);
            if (string.IsNullOrWhiteSpace(invoiceXml))
            {
                throw new InvalidOperationException($"Cannot extract invoice XML from message type '{messageTypeCode}'.");
            }

            EInvoiceXslPreviewHelper.ValidateXml(invoiceXml, "HDon");
            return invoiceXml;
        }

        private async Task<long> ResolveInvoiceIdForSellerPreviewAsync(
            string companyCd,
            EInvoiceMessageReceiveInfo message,
            long? invoiceId)
        {
            if (invoiceId is > 0)
            {
                return invoiceId.Value;
            }

            var referencedMtdiep = Common.NormalizeNullableText(message.MTDTCHIEU);
            if (!string.IsNullOrWhiteSpace(referencedMtdiep))
            {
                var referencedSend = await _messageRepository.GetSendMessageByMtdiepAsync(companyCd, referencedMtdiep);
                if (referencedSend?.INVOICE_ID is > 0)
                {
                    return referencedSend.INVOICE_ID.Value;
                }
            }

            var lookupMtdiep = Common.NormalizeNullableText(message.MTRA_CUU);
            if (!string.IsNullOrWhiteSpace(lookupMtdiep))
            {
                var lookupSend = await _messageRepository.GetSendMessageByMtdiepAsync(companyCd, lookupMtdiep);
                if (lookupSend?.INVOICE_ID is > 0)
                {
                    return lookupSend.INVOICE_ID.Value;
                }
            }

            throw new InvalidOperationException("INVOICE_ID is required to preview invoice transmission message with seller XSL.");
        }

        private async Task<string> TransformInvoiceXmlWithSellerXslAsync(string companyCd, long invoiceId, string invoiceXml)
        {
            var invoice = (await _invoiceRepository.GetHeadersAsync(companyCd, invoiceId, null, null, null)).FirstOrDefault()
                ?? throw new KeyNotFoundException("E-invoice not found");

            var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
            var xsl = Common.NormalizeNullableText(seller.XSL_CONTENT);
            if (string.IsNullOrWhiteSpace(xsl))
            {
                throw new InvalidOperationException("Seller XSL template is not configured.");
            }

            return EInvoiceXmlHtmlTransformService.Transform(
                invoiceXml,
                xsl,
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.LOGO_PATH, "logoImage", _environment),
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.BACKGROUND_PATH, "backgroundImage", _environment),
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.INVOICE_BACKGROUND_PATH, "nenImage", _environment),
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.INVOICE_BORDER_PATH, "vienHdImage", _environment),
                lookupCompanyCd: companyCd);
        }

        private async Task<EInvoiceSellerInfo> ResolveSellerForInvoiceAsync(string companyCd, EInvoiceInfo invoice)
        {
            if (invoice.XSL_ID is > 0)
            {
                var matched = await _sellerRepository.GetSellerWithXslAsync(
                    companyCd,
                    sellerId: invoice.SELLER_ID,
                    xslId: invoice.XSL_ID,
                    includeInactive: true);
                if (matched != null)
                {
                    return matched;
                }

                throw new InvalidOperationException("E-invoice XSL template not found");
            }

            if (invoice.SELLER_ID is > 0)
            {
                var matched = await _sellerRepository.GetSellerWithXslAsync(
                    companyCd,
                    sellerId: invoice.SELLER_ID,
                    khhdon: invoice.KHHDON,
                    includeInactive: true);
                if (matched != null)
                {
                    return matched;
                }
            }

            var khhdon = Common.NormalizeNullableText(invoice.KHHDON);
            var fallbackSellers = (await _sellerRepository.GetSellersAsync(
                companyCd,
                khhdon: khhdon,
                includeInactive: false,
                includeXslContent: true)).ToList();
            if (fallbackSellers.Count == 0)
            {
                fallbackSellers = (await _sellerRepository.GetSellersAsync(
                    companyCd,
                    includeInactive: false,
                    includeXslContent: true)).ToList();
            }

            if (fallbackSellers.Count == 0)
            {
                throw new InvalidOperationException("E-invoice seller is not configured");
            }

            return fallbackSellers.FirstOrDefault(x => x.XSL_IS_DEFAULT == 1) ?? fallbackSellers[0];
        }
    }
}
