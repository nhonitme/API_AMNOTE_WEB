using System.Drawing;
using DevExpress.Drawing;

namespace API_AMNOTE_WEB.Reporting
{
    /// <summary>
    /// Cross-platform text measurement for report layout (header grid height, wrap, etc.).
    /// Uses DevExpress.Drawing (DXBitmap/DXGraphics) so Linux/.NET 10 does not depend on
    /// System.Drawing.Common GDI+, which is unsupported outside Windows.
    /// Results are in report units: hundredths of an inch.
    /// </summary>
    internal static class ReportTextMetrics
    {
        private const float MaxLayoutHeightPoints = 8000f;
        private static int _engineLogged;

        /// <summary>
        /// Measures text size in hundredths of an inch for the given max width.
        /// </summary>
        public static SizeF MeasureSizeHundredths(
            string text,
            DXFont font,
            float maxWidthHundredths,
            bool typographic)
        {
            if (string.IsNullOrEmpty(text))
            {
                return SizeF.Empty;
            }

            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            LogDrawingEngineOnce();

            try
            {
                var format = typographic
                    ? DXStringFormat.CreateGenericTypographic()
                    : DXStringFormat.CreateGenericDefault();

                // Report units are HundredthsOfAnInch; DXGraphics uses Point for measurement
                // (same conversion the previous GDI path used: 1 in = 72 pt = 100 hundredths).
                var maxWidthPoints = Math.Max(
                    1f,
                    DXGraphicsUnitConverter.Convert(
                        maxWidthHundredths / 100f,
                        DXGraphicsUnit.Inch,
                        DXGraphicsUnit.Point));

                using var bitmap = new DXBitmap(1, 1);
                using var graphics = DXGraphics.FromImage(bitmap);
                graphics.PageUnit = DXGraphicsUnit.Point;

                var sizePoints = graphics.MeasureString(
                    text,
                    font,
                    new SizeF(maxWidthPoints, MaxLayoutHeightPoints),
                    format);

                var sizeInches = DXGraphicsUnitConverter.Convert(
                    sizePoints,
                    DXGraphicsUnit.Point,
                    DXGraphicsUnit.Inch);
                return new SizeF(sizeInches.Width * 100f, sizeInches.Height * 100f);
            }
            catch (Exception ex)
            {
                LogWarning(
                    ex,
                    $"DX text measure failed (font={font.Name}, size={font.Size}, widthHi={maxWidthHundredths}, typographic={typographic}). Using safe fallback.");
                return EstimateSizeHundredthsFallback(text, font, maxWidthHundredths);
            }
        }

        public static float MeasureWidthHundredths(string text, DXFont font, bool typographic)
        {
            return MeasureSizeHundredths(text, font, 10000f, typographic).Width;
        }

        /// <summary>
        /// Safe estimate when DX measure fails: wrap by approximate character width and
        /// honor explicit line breaks. Prefer slightly taller than clipped text.
        /// </summary>
        private static SizeF EstimateSizeHundredthsFallback(
            string text,
            DXFont font,
            float maxWidthHundredths)
        {
            var fontSizeHundredths = DXGraphicsUnitConverter.Convert(
                font.Size,
                DXGraphicsUnit.Point,
                DXGraphicsUnit.Inch) * 100f;
            var avgCharWidth = Math.Max(1f, fontSizeHundredths * 0.55f);
            var usableWidth = Math.Max(avgCharWidth, maxWidthHundredths);
            var charsPerLine = Math.Max(1, (int)Math.Floor(usableWidth / avgCharWidth));
            var lineHeight = Math.Max(fontSizeHundredths * 1.35f, fontSizeHundredths + 2f);

            var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            var lines = 0;
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length == 0)
                {
                    lines += 1;
                    continue;
                }

                lines += Math.Max(1, (int)Math.Ceiling(paragraph.Length / (double)charsPerLine));
            }

            lines = Math.Max(1, lines);
            return new SizeF(usableWidth, lines * lineHeight);
        }

        private static void LogDrawingEngineOnce()
        {
            if (Interlocked.Exchange(ref _engineLogged, 1) != 0)
            {
                return;
            }

            try
            {
                var engine = Settings.DrawingEngine;
                Console.WriteLine(
                    $"[ReportTextMetrics] DevExpress DrawingEngine={engine}; OS={Environment.OSVersion.Platform}; " +
                    $"64bit={Environment.Is64BitProcess}");
            }
            catch (Exception ex)
            {
                LogWarning(ex, "Failed to read DevExpress Settings.DrawingEngine.");
            }
        }

        private static void LogWarning(Exception? ex, string message)
        {
            try
            {
                var detail = ex == null
                    ? message
                    : $"{message} | {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Trace.TraceWarning("[ReportTextMetrics] {0}", detail);
                Console.Error.WriteLine($"[ReportTextMetrics] {detail}");
            }
            catch
            {
                // Never let logging break report generation.
            }
        }
    }
}
