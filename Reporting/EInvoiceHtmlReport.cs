using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraReports.UI;
using DevExpress.XtraRichEdit;
using System.Text;

namespace API_AMNOTE_WEB.Reporting
{
    public sealed class EInvoiceHtmlReport : XtraReport
    {
        public EInvoiceHtmlReport(string html, string displayName)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "E-Invoice" : displayName.Trim();
            PaperKind = DXPaperKind.A4;
            Margins = new DXMargins(25, 25, 25, 25);
            Version = "25.2";

            var detail = new DetailBand
            {
                HeightF = 100F,
            };
            Bands.Add(detail);

            var richText = new XRRichText
            {
                LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F),
                WidthF = PageWidth - Margins.Left - Margins.Right,
                CanGrow = true,
                CanShrink = true,
            };
            detail.Controls.Add(richText);

            using var server = new RichEditDocumentServer();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html ?? string.Empty));
            server.LoadDocument(stream, DocumentFormat.Html);
            richText.Rtf = server.RtfText;
        }
    }
}
