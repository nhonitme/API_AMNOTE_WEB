using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Services;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    public sealed record EInvoiceXslPreviewResult(string Xml, string Xsl, string Html);

    public sealed record EInvoiceXslPreviewRequest<TEntity>(
        string DocumentLabel,
        string IdFieldName,
        string DefaultTemplateCd,
        string NotFoundMessage,
        Func<string, long, Task<TEntity?>> LoadEntityAsync,
        Func<TEntity, string?> GetTemplateCd,
        Func<TEntity, string> ResolveXml);

    public static class EInvoiceXslPreviewHelper
    {
        public const string TemplateTypeXsl = "XSL";

        public static async Task<EInvoiceXslPreviewResult> BuildAsync<TEntity>(
            string companyCd,
            long documentId,
            EInvoiceXslPreviewRequest<TEntity> request,
            IEInvoiceTemplateRepository templateRepository)
        {
            ValidateCompany(companyCd);
            if (documentId <= 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument(request.IdFieldName);
            }

            var entity = await request.LoadEntityAsync(companyCd, documentId)
                ?? throw new KeyNotFoundException(request.NotFoundMessage);

            var templateCd = Common.NormalizeNullableText(request.GetTemplateCd(entity)) ?? request.DefaultTemplateCd;
            var template = await templateRepository.GetActiveTemplateAsync(TemplateTypeXsl, templateCd)
                ?? throw new InvalidOperationException($"{request.DocumentLabel} XSL template '{templateCd}' is not configured.");

            var xsl = Common.NormalizeNullableText(template.CONTENT);
            if (string.IsNullOrWhiteSpace(xsl))
            {
                throw new InvalidOperationException($"{request.DocumentLabel} XSL template '{templateCd}' is empty.");
            }

            var xml = request.ResolveXml(entity);
            var html = EInvoiceXmlHtmlTransformService.Transform(
                xml,
                xsl,
                lookupCompanyCd: companyCd);

            return new EInvoiceXslPreviewResult(xml, xsl, html);
        }

        public static string ResolveStoredOrBuiltXml<TEntity>(
            TEntity entity,
            Func<TEntity, string?> getStoredXml,
            Func<TEntity, string?> buildXml,
            string buildFailureMessage)
        {
            var storedXml = Common.NormalizeNullableText(getStoredXml(entity));
            if (!string.IsNullOrWhiteSpace(storedXml))
            {
                ValidateXml(storedXml, "XML");
                return storedXml;
            }

            var rawXml = buildXml(entity);
            if (string.IsNullOrWhiteSpace(rawXml))
            {
                throw new InvalidOperationException(buildFailureMessage);
            }

            ValidateXml(rawXml, "RAW_XML");
            return rawXml;
        }

        public static void ValidateXml(string xml, string fieldName)
        {
            try
            {
                XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{fieldName} is invalid XML: {ex.Message}");
            }
        }

        public static void ValidateCompany(string companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new UnauthorizedAccessException("Company code not found");
            }
        }
    }
}
