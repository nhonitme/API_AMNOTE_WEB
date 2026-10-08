using System.Data;
using System.Drawing;
using System.Globalization;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using API_AMNOTE_WEB.Services;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;

namespace API_AMNOTE_WEB.Reporting;

/// <summary>Two-table appendix matching the supplied desktop export.</summary>
public sealed class TaxReductionAppendixReport : XtraReport
{
    public TaxReductionAppendixReport(ReportConfigurationInfo config, DataTable data,
        ReportCompanyContext company, IReadOnlyDictionary<string, string> query)
    {
        PaperKind = DevExpress.Drawing.Printing.DXPaperKind.A4;
        Margins = new System.Drawing.Printing.Margins(config.MARGIN_LEFT ?? 40, config.MARGIN_RIGHT ?? 40,
            config.MARGIN_TOP ?? 25, config.MARGIN_BOTTOM ?? 25);
        Font = new DXFont(config.FONT_FAMILY ?? "Arial", (float)(config.FONT_SIZE ?? 9));
        RequestParameters = false;
        DisplayName = "Phụ lục giảm thuế GTGT";
        var width = PageWidth - Margins.Left - Margins.Right;
        var header = new ReportHeaderBand { HeightF = 155 };
        Bands.Add(header);
        var title = config.ELEMENTS.FirstOrDefault(x => x.ITEM_KEY == "TITLE")?.CAPTION ?? "PHỤ LỤC GIẢM THUẾ GIÁ TRỊ GIA TĂNG";
        header.Controls.Add(Label(title, width, 0, 45, true, TextAlignment.MiddleCenter));
        query.TryGetValue("fromYmd", out var from); query.TryGetValue("toYmd", out var to);
        static string DateText(string? value) => DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date.ToString("dd/MM/yyyy") : value ?? "";
        header.Controls.Add(Label($"Kỳ báo cáo: {DateText(from)} – {DateText(to)}", width, 45, 25, false, TextAlignment.MiddleCenter));
        header.Controls.Add(Label($"[01] Tên người nộp thuế: {company.CompanyNameText}", width, 75, 25));
        header.Controls.Add(Label($"[02] Mã số thuế: {company.CompanyInfo.TAX_CD}", width, 100, 25));
        header.Controls.Add(Label("Đơn vị tiền tệ: Việt Nam đồng", width, 125, 25, false, TextAlignment.MiddleRight));
        Bands.Add(new DetailBand { HeightF = 0 });
        AddSection(data, 1, "I. Hàng hóa, dịch vụ mua vào trong kỳ được áp dụng mức thuế suất thuế giá trị gia tăng 8%",
            new[] { "STT", "Tên hàng hóa, dịch vụ", "Giá trị mua vào chưa có thuế GTGT", "Thuế GTGT mua vào được khấu trừ" },
            new[] { "STT", "PRODUCT_NAME", "BILL_AMOUNT", "VAT_AMOUNT" }, new[] { .07, .49, .24, .20 }, width);
        AddSection(data, 2, "II. Hàng hóa, dịch vụ bán ra trong kỳ",
            new[] { "STT", "Tên hàng hóa, dịch vụ", "Giá trị chưa có thuế GTGT", "Thuế suất theo quy định", "Thuế suất sau giảm", "Thuế GTGT được giảm" },
            new[] { "STT", "PRODUCT_NAME", "BILL_AMOUNT", "ORIGINAL_RATE", "REDUCED_RATE", "REDUCTION_AMOUNT" },
            new[] { .06, .36, .20, .10, .10, .18 }, width);
        var buyTax = data.AsEnumerable().Where(r => r.Field<int>("TYPE") == 1).Sum(r => r.Field<decimal>("VAT_AMOUNT"));
        var reduction = data.AsEnumerable().Where(r => r.Field<int>("TYPE") == 2).Sum(r => r.Field<decimal>("REDUCTION_AMOUNT"));
        var footer = new ReportFooterBand { HeightF = 65 };
        footer.Controls.Add(Label($"III. Chênh lệch thuế GTGT của hàng hóa, dịch vụ bán ra và mua vào trong kỳ: {(reduction - buyTax):N0} đồng", width, 0, 60, true));
        Bands.Add(footer);
        var pageFooter = new PageFooterBand { HeightF = 25 };
        pageFooter.Controls.Add(new XRPageInfo { WidthF = width, HeightF = 25, TextAlignment = TextAlignment.MiddleRight,
            PageInfo = PageInfo.NumberOfTotal, TextFormatString = "Trang {0}/{1}" });
        Bands.Add(pageFooter);
    }

    private void AddSection(DataTable source, int type, string title, string[] captions, string[] fields, double[] weights, float width)
    {
        var table = source.Clone();
        if (!table.Columns.Contains("STT")) table.Columns.Add("STT", typeof(int));
        if (!table.Columns.Contains("ORIGINAL_RATE")) table.Columns.Add("ORIGINAL_RATE", typeof(int));
        if (!table.Columns.Contains("REDUCED_RATE")) table.Columns.Add("REDUCED_RATE", typeof(int));
        foreach (var sourceRow in source.AsEnumerable().Where(r => r.Field<int>("TYPE") == type))
        {
            table.ImportRow(sourceRow);
            var row = table.Rows[table.Rows.Count - 1];
            row["STT"] = table.Rows.Count; row["ORIGINAL_RATE"] = 10; row["REDUCED_RATE"] = 8;
            row["BILL_AMOUNT"] = TaxReductionAppendixCalculator.Round((decimal)row["BILL_AMOUNT"]);
        }
        var section = new DetailReportBand { DataSource = table, Level = type - 1 };
        var heading = new GroupHeaderBand { HeightF = 115, RepeatEveryPage = true };
        heading.Controls.Add(Label(title, width, 0, 40, true));
        heading.Controls.Add(Row(captions, weights, width, 40, 75, true));
        var detail = new DetailBand { HeightF = 25 };
        var detailTable = Row(fields, weights, width, 0, 25, false);
        for (var i = 0; i < fields.Length; i++)
        {
            var cell = detailTable.Rows[0].Cells[i];
            cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"[{fields[i]}]"));
            cell.TextFormatString = fields[i].EndsWith("AMOUNT") ? "{0:N0}" : "{0}";
            cell.TextAlignment = fields[i].EndsWith("AMOUNT") ? TextAlignment.MiddleRight : TextAlignment.MiddleLeft;
        }
        detail.Controls.Add(detailTable);
        var totals = new GroupFooterBand { HeightF = 28 };
        var values = fields.Select((field, i) => i == 1 ? "Tổng cộng" : field.EndsWith("AMOUNT")
            ? table.AsEnumerable().Sum(r => r.Field<decimal>(field)).ToString("N0") : "").ToArray();
        totals.Controls.Add(Row(values, weights, width, 0, 28, true));
        section.Bands.AddRange(new Band[] { heading, detail, totals });
        Bands.Add(section);
    }

    private static XRLabel Label(string text, float width, float top, float height, bool bold = false,
        TextAlignment align = TextAlignment.MiddleLeft) => new() { Text = text, WidthF = width, TopF = top,
        HeightF = height, CanGrow = true, Multiline = true, TextAlignment = align,
        Font = new DXFont("Arial", bold ? 10 : 9, bold ? DXFontStyle.Bold : DXFontStyle.Regular) };
    private static XRTable Row(string[] values, double[] weights, float width, float top, float height, bool bold)
    {
        var table = new XRTable { WidthF = width, TopF = top, HeightF = height, Borders = BorderSide.All };
        table.BeginInit();
        var row = new XRTableRow();
        for (var i = 0; i < values.Length; i++) row.Cells.Add(new XRTableCell { Text = values[i], Weight = weights[i],
            CanGrow = true, Multiline = true, Padding = new PaddingInfo(3, 3, 2, 2),
            Font = new DXFont("Arial", 9, bold ? DXFontStyle.Bold : DXFontStyle.Regular) });
        table.Rows.Add(row); table.EndInit(); return table;
    }
}
