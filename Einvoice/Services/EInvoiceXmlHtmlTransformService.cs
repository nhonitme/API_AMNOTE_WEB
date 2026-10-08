using Saxon.Api;
using System.Text;
using System.Xml;

namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoiceXmlHtmlTransformService
    {
        public static string Transform(
            string xmlContent,
            string xslContent,
            string? logoImage = null,
            string? backgroundImage = null,
            string? nenImage = null,
            string? vienHdImage = null,
            string? lookupCompanyCd = null,
            IReadOnlyDictionary<string, string>? extraParameters = null)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new InvalidOperationException("Invoice XML is empty.");
            }

            if (string.IsNullOrWhiteSpace(xslContent))
            {
                throw new InvalidOperationException("Seller XSL template is not configured.");
            }

            // Strip BOM / ZWSP so XmlReader and LooksLikeXml see the same root.
            xslContent = StripLeadingNoise(xslContent);
            xmlContent = StripLeadingNoise(xmlContent);

            if (!LooksLikeXml(xslContent))
            {
                var preview = xslContent.Length > 80 ? xslContent[..80] : xslContent;
                throw new InvalidOperationException(
                    "Seller XSL template is not valid XML (root parse failed). Content preview: " + preview);
            }

            if (!LooksLikeXml(xmlContent))
            {
                var preview = xmlContent.Length > 80 ? xmlContent[..80] : xmlContent;
                throw new InvalidOperationException(
                    "Invoice XML is not valid XML (root parse failed). Content preview: " + preview);
            }

            try
            {
                var processor = new Processor();
                var compiler = processor.NewXsltCompiler();
                compiler.BaseUri = new Uri("file:///einvoice.xsl");

                using var xslReader = XmlReader.Create(new StringReader(xslContent), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });

                var executable = compiler.Compile(xslReader);
                var transformer = executable.Load();

                void SetParameter(string name, string? value)
                {
                    transformer.SetParameter(new QName(name), new XdmAtomicValue(value ?? string.Empty));
                }

                SetParameter("logoImage", logoImage);
                SetParameter("backgroundImage", backgroundImage);
                SetParameter("nenImage", nenImage);
                SetParameter("vienHdImage", vienHdImage);
                if (!string.IsNullOrWhiteSpace(lookupCompanyCd))
                {
                    SetParameter("lookupCompanyCd", lookupCompanyCd);
                }

                if (extraParameters != null)
                {
                    foreach (var pair in extraParameters)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                        SetParameter(pair.Key, pair.Value ?? string.Empty);
                    }
                }

                var hasQrImageParam = extraParameters != null
                    && extraParameters.Keys.Any(k => string.Equals(k, "qrCodeImage", StringComparison.OrdinalIgnoreCase));
                if (!hasQrImageParam)
                {
                    var qrPayload = EInvoiceQrCodeImageHelper.TryExtractPayload(xmlContent);
                    var qrImage = EInvoiceQrCodeImageHelper.TryCreatePngDataUri(qrPayload);
                    if (!string.IsNullOrWhiteSpace(qrImage))
                    {
                        SetParameter("qrCodeImage", qrImage);
                    }
                }

                using var xmlReader = XmlReader.Create(new StringReader(xmlContent), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });

                var input = processor.NewDocumentBuilder().Build(xmlReader);
                transformer.InitialContextNode = input;

                var serializer = processor.NewSerializer();
                serializer.SetOutputProperty(Serializer.METHOD, "html");
                serializer.SetOutputProperty(Serializer.ENCODING, "UTF-8");

                using var writer = new StringWriter(new StringBuilder());
                serializer.OutputWriter = writer;
                transformer.Run(serializer);
                return writer.ToString();
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"XSL transform failed: {ex.Message}", ex);
            }
        }

        private static string StripLeadingNoise(string content)
        {
            return content.TrimStart('\uFEFF', '\u200B', '\u00A0', ' ', '\t', '\r', '\n');
        }

        private static bool LooksLikeXml(string content)
        {
            var trimmed = StripLeadingNoise(content);
            return trimmed.Length > 0 && trimmed[0] == '<';
        }
    }
}
