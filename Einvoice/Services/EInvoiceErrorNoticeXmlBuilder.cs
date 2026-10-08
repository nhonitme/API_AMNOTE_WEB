using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoiceErrorNoticeXmlBuilder
    {
        public static string Build(EInvoiceErrorNoticeInfo notice)
        {
            var details = notice.DETAILS
                .Where(x => x.ISDEL != 1)
                .OrderBy(x => x.STT)
                .Select(BuildDetail)
                .ToList();

            var dlTBao = new XElement("DLTBao",
                Text("PBan", notice.PBAN),
                Text("MSo", notice.MSO),
                Text("Ten", notice.TEN),
                Number("Loai", notice.LOAI),
                Text("MCQT", notice.MCQT),
                Text("TCQT", notice.TCQT),
                Text("So", notice.SO),
                Date("NTBCCQT", notice.NTBCCQT),
                Text("MST", notice.MST),
                Text("TNNT", notice.TNNT),
                Text("DDanh", notice.DDANH),
                Date("NTBao", notice.NTBAO),
                new XElement("DSHDon", details)
            );

            var dlTBaoId = Common.NormalizeNullableText(notice.MTDIEP);
            if (HasText(dlTBaoId))
                dlTBao.SetAttributeValue("Id", dlTBaoId);

            var root = new XElement("TBao",
                dlTBao,
                new XElement("DSCKS",
                    new XElement("NNT",
                        new XElement("Signature")
                    )
                )
            );

            return XmlSerializationHelper.Serialize(new XDocument(root));
        }

        private static XElement BuildDetail(EInvoiceErrorNoticeDetail detail)
        {
            return new XElement("HDon",
                Number("STT", detail.STT),
                Text("MCCQT", detail.MCCQT),
                Text("KHMSHDon", detail.KHMSHDON),
                Text("KHHDon", detail.KHHDON),
                Text("SHDon", detail.SHDON),
                Date("Ngay", detail.NGAY),
                Number("LADHDDT", detail.LADHDDT),
                Text("LDo", detail.LDO)
            );
        }

        private static XElement Text(string name, string? value)
        {
            return new XElement(name, Common.NormalizeNullableText(value) ?? string.Empty);
        }

        private static XElement Number(string name, int? value)
        {
            return new XElement(name, value?.ToString() ?? string.Empty);
        }

        private static XElement Date(string name, DateTime? value)
        {
            return new XElement(name, value.HasValue ? value.Value.ToString("yyyy-MM-dd") : string.Empty);
        }

        private static bool HasText(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
