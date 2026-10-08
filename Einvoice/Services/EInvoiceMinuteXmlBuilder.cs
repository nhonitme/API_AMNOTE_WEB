using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal sealed class EInvoiceMinuteXmlBuilder
    {
        private readonly EInvoiceDecimalFormatter _formatter;
        private readonly string _currencyCode;

        public EInvoiceMinuteXmlBuilder(EInvoiceDecimalFormatter formatter, string? currencyCode = "VND")
        {
            _formatter = formatter;
            _currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "VND" : currencyCode.Trim().ToUpperInvariant();
        }

        public string Build(EInvoiceMinuteInfo minute)
        {
            var root = new XElement("BBan",
                BuildContent(minute, includeId: true),
                new XElement("DSCKS",
                    new XElement("NBan", new XElement("Signature")),
                    new XElement("NMua", new XElement("Signature"))
                )
            );

            return XmlSerializationHelper.Serialize(new XDocument(root));
        }

        public string BuildContentXml(EInvoiceMinuteInfo minute)
        {
            return XmlSerializationHelper.Serialize(new XDocument(BuildContent(minute)));
        }

        public static void ValidateAdditionalInfoXml(string? xml)
        {
            _ = BuildAdditionalInfo(xml);
        }

        private XElement BuildContent(EInvoiceMinuteInfo minute, bool includeId = false)
        {
            var children = new object?[]
            {
                Text("PBan", minute.PBAN),
                Text("TBBan", minute.TBBAN),
                Text("SBBan", minute.SBBAN),
                Date("NBBan", minute.NBBAN),
                Number("TCHDon", minute.TCHDON),
                Text("NBan", minute.NBAN),
                Text("MSTNBan", minute.MSTNBAN),
                Text("DCNban", minute.DCNBAN),
                Text("NMua", minute.NMUA),
                Text("MSTNMua", minute.MSTNMUA),
                Text("DCNmua", minute.DCNMUA),
                Text("KHMSHDon", minute.KHMSHDON),
                Text("KHHDon", minute.KHHDON),
                Text("SHDon", minute.SHDON),
                Date("NLap", minute.NLAP),
                BuildReasons(minute.REASONS),
                BuildLineSection("DSHHDVuTruoc", minute.LINES_BEFORE, minute.TOTAL_BEFORE),
                BuildPayment("TToan", minute.LINES_BEFORE, minute.TOTAL_BEFORE),
                BuildLineSection("DSHHDVuSau", minute.LINES_AFTER, minute.TOTAL_AFTER),
                BuildPayment("ATToan", minute.LINES_AFTER, minute.TOTAL_AFTER),
                BuildAdditionalInfo(minute.TTKHAC_XML)
            };

            var content = new XElement("NDBBan",
                new XElement("TTChung", children.Where(x => x != null)));

            if (includeId && minute.BBAN_ID > 0)
            {
                content.SetAttributeValue("Id", $"BBan-{minute.BBAN_ID.ToString(CultureInfo.InvariantCulture)}");
            }

            return content;
        }

        private static XElement BuildReasons(IEnumerable<EInvoiceMinuteReason> reasons)
        {
            return new XElement("DSLDTDoi",
                reasons
                    .Where(x => x.ISDEL != 1)
                    .OrderBy(x => x.SORT_ORDER)
                    .Select(x => Text("LDo", x.LDO))
            );
        }

        private XElement? BuildLineSection(string sectionName, IEnumerable<EInvoiceMinuteLine> lines, EInvoiceMinuteLine? totalLine)
        {
            var elements = lines
                .Where(x => x.ISDEL != 1)
                .OrderBy(x => x.SORT_ORDER)
                .Select(BuildLine)
                .Where(x => x != null)
                .Cast<XElement>()
                .ToList();

            if (totalLine is { ISDEL: not 1 })
            {
                var totalElement = BuildLine(totalLine);
                if (totalElement != null)
                    elements.Add(totalElement);
            }

            return elements.Count > 0 ? new XElement(sectionName, elements) : null;
        }

        private XElement? BuildPayment(string elementName, IEnumerable<EInvoiceMinuteLine> lines, EInvoiceMinuteLine? totalLine)
        {
            var activeLines = lines.Where(x => x.ISDEL != 1).ToList();
            if (activeLines.Count == 0 && totalLine is not { ISDEL: not 1 })
                return null;

            var total = totalLine is { ISDEL: not 1 } ? totalLine : null;
            var children = new List<XElement?>
            {
                BuildTaxSummary(activeLines, total),
                DecimalHeaderElement("TgTCThue", "TGTCTHUE", total?.THTIEN ?? 0m),
                DecimalHeaderElement("TGTKCThue", "TGTKCTHUE", 0m),
                DecimalHeaderElement("TgTThue", "TGTTTHUE", total?.TTHUE ?? 0m),
                DecimalHeaderElement("TTCKTMai", "TTCKTMAI", SumStoredCommercialDiscount(activeLines)),
                DecimalHeaderElement("TGTKhac", "TGTKHAC", 0m),
                DecimalHeaderElement("TgTTTBSo", "TGTTTBSO", total?.TSAUTHUE ?? 0m),
                // Không truyền reportLanguage: TgTTTBChu luôn là tiếng Việt.
                Text("TgTTTBChu", ReportCurrencyHelper.ConvertAmountToWords(total?.TSAUTHUE ?? 0m, _currencyCode))
            };

            var filtered = children.Where(x => x != null).Cast<XElement>().ToList();
            return filtered.Count > 0 ? new XElement(elementName, filtered) : null;
        }

        private XElement? BuildTaxSummary(IReadOnlyList<EInvoiceMinuteLine> lines, EInvoiceMinuteLine? totalLine)
        {
            var groups = lines
                .GroupBy(line => Common.NormalizeNullableText(line.TSUAT) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TaxRateSummaryRow
                {
                    TaxRate = group.Key,
                    Amount = group.Sum(line => line.THTIEN ?? 0m),
                    TaxAmount = group.Sum(line => line.TTHUE ?? 0m)
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.TaxRate) || row.Amount != 0m || row.TaxAmount != 0m)
                .OrderBy(row => row.TaxRate, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count == 0)
            {
                var fallbackTaxRate = lines
                    .Select(line => Common.NormalizeNullableText(line.TSUAT))
                    .FirstOrDefault(rate => !string.IsNullOrWhiteSpace(rate));

                if (string.IsNullOrWhiteSpace(fallbackTaxRate) &&
                    totalLine?.THTIEN == null &&
                    totalLine?.TTHUE == null)
                {
                    return null;
                }

                groups.Add(new TaxRateSummaryRow
                {
                    TaxRate = fallbackTaxRate ?? string.Empty,
                    Amount = totalLine?.THTIEN ?? 0m,
                    TaxAmount = totalLine?.TTHUE ?? 0m
                });
            }

            var rateElements = groups
                .Select(BuildTaxRateElement)
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            return rateElements.Count > 0 ? new XElement("THTTLTSuat", rateElements) : null;
        }

        private XElement? BuildTaxRateElement(TaxRateSummaryRow row)
        {
            var children = new List<XElement?>
            {
                Text("TSuat", row.TaxRate),
                DecimalDetailElement("ThTien", "THTIEN", row.Amount),
                DecimalHeaderElement("TThue", "TGTTTHUE", row.TaxAmount)
            };

            var filtered = children.Where(x => x != null).Cast<XElement>().ToList();
            return filtered.Count > 0 ? new XElement("LTSuat", filtered) : null;
        }

        private static decimal SumStoredCommercialDiscount(IEnumerable<EInvoiceMinuteLine> lines)
        {
            return lines.Sum(line => line.STCKHAU ?? 0m);
        }

        private sealed class TaxRateSummaryRow
        {
            public string TaxRate { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public decimal TaxAmount { get; set; }
        }

        private XElement? BuildLine(EInvoiceMinuteLine line)
        {
            var children = new List<XElement?>
            {
                Number("TChat", line.TCHAT),
                Number("STT", line.STT),
                Text("MHHDVu", line.MHHDVU),
                Text("THHDVu", line.THHDVU),
                Text("DVTinh", line.DVTINH),
                DecimalDetailElement("SLuong", "SLUONG", line.SLUONG),
                DecimalDetailElement("DGia", "DGIA", line.DGIA),
                DecimalDetailElement("TLCKhau", "TLCKHAU", line.TLCKHAU),
                DecimalDetailElement("STCKhau", "STCKHAU", line.STCKHAU),
                DecimalDetailElement("ThTien", "THTIEN", line.THTIEN),
                Text("TSuat", line.TSUAT),
                DecimalDetailElement("TThue", "TTHUE", line.TTHUE),
                DecimalDetailElement("TSauThue", "TSAUTHUE", line.TSAUTHUE),
                BuildLineAdditionalInfo(line.EXTRA_JSON)
            };

            if (line.DETAIL_ID.HasValue && line.DETAIL_ID.Value > 0)
            {
                children.Insert(0, Number("DetailId", line.DETAIL_ID));
            }

            var filtered = children.Where(x => x != null).Cast<XElement>().ToList();
            return filtered.Count > 0 ? new XElement("HHDVu", filtered) : null;
        }

        private static object? BuildAdditionalInfo(string? xml)
        {
            var normalized = Common.NormalizeNullableText(xml);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            try
            {
                var element = XElement.Parse(normalized, LoadOptions.PreserveWhitespace);
                return element.Name.LocalName == "TTKhac" ? element : new XElement("TTKhac", element);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"TTKHAC_XML is invalid XML: {ex.Message}");
            }
        }

        private static XElement? BuildLineAdditionalInfo(string? extraJson)
        {
            var normalized = Common.NormalizeNullableText(extraJson);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            if (normalized.TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                try
                {
                    var element = XElement.Parse(normalized, LoadOptions.PreserveWhitespace);
                    return element.Name.LocalName == "TTKhac" ? element : new XElement("TTKhac", element);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private XElement? DecimalHeaderElement(string name, string fieldKey, decimal? value)
        {
            var text = _formatter.FormatHeader(fieldKey, value, _currencyCode);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return new XElement(name, text);
        }

        private XElement? DecimalDetailElement(string name, string fieldKey, decimal? value)
        {
            var text = _formatter.FormatDetail(fieldKey, value, _currencyCode);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return new XElement(name, text);
        }

        private static XElement Text(string name, string? value)
        {
            return new XElement(name, Common.NormalizeNullableText(value) ?? string.Empty);
        }

        private static XElement? Number(string name, int? value)
        {
            return value.HasValue ? new XElement(name, value.Value.ToString(CultureInfo.InvariantCulture)) : null;
        }

        private static XElement? Number(string name, long? value)
        {
            return value.HasValue ? new XElement(name, value.Value.ToString(CultureInfo.InvariantCulture)) : null;
        }

        private static XElement Number(string name, int value)
        {
            return new XElement(name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static XElement Date(string name, DateTime? value)
        {
            return new XElement(name, value.HasValue ? value.Value.ToString("yyyy-MM-dd") : string.Empty);
        }
    }
}
