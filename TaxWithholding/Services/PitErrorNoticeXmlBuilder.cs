using System.Globalization;
using System.Xml.Linq;
using static API_AMNOTE_WEB.TaxWithholding.PitXmlElements;
namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitErrorNoticeXmlBuilder
{
    public static string Build(PitErrorNoticeDto data, long id, int version)
    {
        PitErrorNoticeValidator.Validate(data);
        var payload = new XElement("DLTBao", new XAttribute("Id", $"PIT-{id}-v{version}"),
            new XElement("PBan", "2.1.1"),
            new XElement("MSo", "04/SS-CTĐT"),
            new XElement("Ten", "Thông báo chứng từ điện tử đã lập sai"),
            Value("Loai", data.LOAI),
            Value("So", data.SO),
            Value("NTBCCQT", data.NTBCCQT),
            Value("MCQT", data.MCQT),
            Value("TCQT", data.TCQT),
            Value("TNNT", data.TNNT),
            Value("MST", data.MST),
            Value("DDanh", data.DDANH),
            Value("NTBao", data.NTBAO));
        payload.Add(new XElement("DSCTu", data.Items.Select((item, i) => new XElement("CTu", new XElement("STT", i + 1), new XElement("KHMSCTu", item.KHMSCTU), new XElement("KHCTu", item.KHCTU), new XElement("SCTu", item.SCTU), new XElement("NLap", item.NLAP.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), new XElement("LCTDT", item.LCTDT), string.IsNullOrWhiteSpace(item.LDO) ? null : new XElement("LDo", item.LDO)))));
        return Serialize("TBao", payload, "NNT");
    }
}
