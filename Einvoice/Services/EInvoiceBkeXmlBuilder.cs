using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    /// <summary>XML bảng kê 01/BK-ĐCTT theo QD 1233 Khoản 7 Mục III Phần II.</summary>
    internal static class EInvoiceBkeXmlBuilder
    {
        public static string Build(EInvoiceBkeInfo bke)
        {
            var ndBke = BuildElement("NDBKe",
                BuildElement("TTChung",
                    Text("PBan", string.IsNullOrWhiteSpace(bke.PBAN) ? "2.1.1" : bke.PBAN),
                    Text("TBKe", bke.TBKE),
                    Text("KHMBKe", string.IsNullOrWhiteSpace(bke.KHMBKE) ? "01/BK-ĐCTT" : bke.KHMBKE),
                    Text("SBKe", bke.SBKE),
                    Date("NBKe", bke.NBKE),
                    Number("TCHDon", bke.TCHDON),
                    Text("NBan", bke.NBAN),
                    Text("MSTNBan", bke.MSTNBAN),
                    Text("DCNBan", bke.DCNBAN),
                    Number("TCTCNHang", bke.TCTCNHANG),
                    Text("NMua", bke.NMUA),
                    Text("MSTNMua", bke.MSTNMUA),
                    Text("DCNMua", bke.DCNMUA),
                    BuildReasons(bke.REASONS),
                    ParseOptionalXml(bke.TTKHAC_XML, "TTKhac")),
                BuildDetails(bke.DETAILS)) ?? new XElement("NDBKe");

            // Plugin ký trên NDBKe Id (tương tự DLHDon / NDBBan).
            var ndBkeId = ResolveNdBkeId(bke);
            if (!string.IsNullOrWhiteSpace(ndBkeId))
            {
                ndBke.SetAttributeValue("Id", ndBkeId);
            }

            var document = new XDocument(
                BuildElement("BKe",
                    ndBke,
                    BuildElement("DSCKS",
                        new XElement("NBan", new XElement("Signature")),
                        new XElement("NMua", new XElement("Signature")))) ?? new XElement("BKe"));

            return XmlSerializationHelper.Serialize(document);
        }

        private static string ResolveNdBkeId(EInvoiceBkeInfo bke)
        {
            if (bke.BKE_ID > 0)
            {
                return "BKe-" + bke.BKE_ID.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var sbke = Common.NormalizeNullableText(bke.SBKE);
            if (!string.IsNullOrWhiteSpace(sbke))
            {
                return "BKe-" + sbke.Replace(' ', '-');
            }

            return "BKe-" + Guid.NewGuid().ToString("N");
        }

        private static XElement? BuildReasons(IEnumerable<EInvoiceBkeReason> reasons)
        {
            var rows = reasons
                .Where(x => x.ISDEL != 1 && !string.IsNullOrWhiteSpace(x.LDO))
                .OrderBy(x => x.SORT_ORDER)
                .Select(x => Text("LDo", x.LDO))
                .Where(x => x != null)
                .Cast<XElement>()
                .ToList();

            return rows.Count == 0 ? null : new XElement("DSLDTDoi", rows);
        }

        private static XElement? BuildDetails(IEnumerable<EInvoiceBkeDetail> details)
        {
            var rows = details
                .Where(x => x.ISDEL != 1)
                .OrderBy(x => x.STT ?? int.MaxValue)
                .Select(BuildDetail)
                .Where(x => x != null)
                .Cast<XElement>()
                .ToList();

            return rows.Count == 0 ? null : new XElement("DSHHDVu", rows);
        }

        private static XElement? BuildDetail(EInvoiceBkeDetail detail)
        {
            return BuildElement("HHDVu",
                BuildElement("TTTDChinh",
                    Number("STT", detail.STT),
                    Text("KHMSHDon", detail.KHMSHDON),
                    Text("KHHDon", detail.KHHDON),
                    Text("SHDon", detail.SHDON),
                    Text("THHDVGoc", detail.THHDVGOC),
                    Decimal("SLGoc", detail.SLGOC),
                    Decimal("DGGoc", detail.DGGOC),
                    Decimal("ThTGoc", detail.THTGOC),
                    Text("TSGoc", detail.TSGOC),
                    Decimal("TTGoc", detail.TTGOC),
                    Decimal("TgTKGoc", detail.TGTKGOC),
                    Decimal("TgTSTGoc", detail.TGTSTGOC)),
                BuildElement("TTSDChinh",
                    Text("THHDVTDoi", detail.THHDVTDOI),
                    Decimal("SLTDoi", detail.SLTDOI),
                    Decimal("DGTDoi", detail.DGTDOI),
                    Decimal("ThTTDoi", detail.THTTDOI),
                    Text("TSTDoi", detail.TSTDOI),
                    Decimal("TTTDoi", detail.TTTDOI),
                    Decimal("TgTTDoi", detail.TGTTDOI),
                    Decimal("TgTSTTDoi", detail.TGTSTTDOI)),
                BuildElement("CLech",
                    Decimal("TgTCTCLech", detail.TGTCTCLECH),
                    Decimal("TgTTCLech", detail.TGTTCLECH),
                    Decimal("TgTKCLech", detail.TGTKCLECH),
                    Decimal("TgTTTCLech", detail.TGTTTCLECH)));
        }

        private static XElement? Text(string name, string? value)
        {
            var text = Common.NormalizeNullableText(value);
            return string.IsNullOrWhiteSpace(text) ? null : new XElement(name, text);
        }

        private static XElement? Number(string name, int? value)
        {
            return value.HasValue ? new XElement(name, value.Value) : null;
        }

        private static XElement? Decimal(string name, decimal? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return new XElement(name, value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static XElement? Date(string name, DateTime? value)
        {
            return value.HasValue ? new XElement(name, value.Value.ToString("yyyy-MM-dd")) : null;
        }

        private static XElement? BuildElement(string name, params XElement?[]? children)
        {
            if (children == null)
            {
                return null;
            }

            var list = children.Where(x => x != null).Cast<XElement>().ToList();
            return list.Count == 0 ? null : new XElement(name, list);
        }

        private static XElement? ParseOptionalXml(string? xml, string rootName)
        {
            var text = Common.NormalizeNullableText(xml);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                var element = XElement.Parse(text, LoadOptions.PreserveWhitespace);
                return string.Equals(element.Name.LocalName, rootName, StringComparison.OrdinalIgnoreCase)
                    ? element
                    : new XElement(rootName, element);
            }
            catch
            {
                return null;
            }
        }
    }
}
