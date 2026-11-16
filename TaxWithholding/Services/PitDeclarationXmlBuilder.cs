using System.Globalization;
using System.Xml.Linq;
using static API_AMNOTE_WEB.TaxWithholding.PitXmlElements;
namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitDeclarationXmlBuilder
{
    public static string Build(PitDeclarationDto data, long id, int version)
    {
        PitDeclarationValidator.Validate(data);
        var payload = new XElement("DLTKhai", new XAttribute("Id", $"PIT-{id}-v{version}"),
            new XElement("TTChung",
                new XElement("PBan", "2.1.1"),
                new XElement("MSo", "01/ĐKTĐ-CTĐT"),
                new XElement("Ten", "Tờ khai đăng ký sử dụng chứng từ khấu trừ TNCN"),
                Value("HThuc", data.HTHUC),
                Value("TNNT", data.TNNT),
                Value("MST", data.MST),
                Value("CQTQLy", data.CQTQLY),
                Value("MCQTQLy", data.MCQTQLY),
                Value("NLHe", data.NLHE),
                Value("DCLHe", data.DCLHE),
                Value("DCTDTu", data.DCTDTU),
                Value("DTLHe", data.DTLHE),
                Value("DDanh", data.DDANH),
                Value("NLap", data.NLAP)));
        payload.Add(new XElement("NDTKhai",
            new XElement("DTPHanh", new XElement("TCCNPHanh", 1), new XElement("CQTPHanh", 0)),
            new XElement("LHSDung", new XElement("CTTNCNhan", 1), new XElement("BLTPLPKIn", 0), new XElement("BLTPLPIn", 0), new XElement("BLTTPLPhi", 0)),
            new XElement("HTGDLCTDT", new XElement("CDLQCCQT", 0), new XElement("CDLQTCTN", 1), new XElement("CDLQTCTNUT", 0), new XElement("CDLTTiep", 0), new XElement("CDLBTHBLai", 0)),
            new XElement("DSCTSSDung", data.Certificates.Select((cert, i) => new XElement("CTS", new XElement("STT", i + 1), new XElement("TTChuc", cert.TTCHUC.Trim()), new XElement("Seri", cert.SERI.Trim()),
                new XElement("TNgay", XmlDateTime(cert.TNGAY)), new XElement("DNgay", XmlDateTime(cert.DNGAY)), new XElement("HThuc", cert.HTHUC))))));
        return Serialize("TKhai", payload, "NNT");
    }
    private static string XmlDateTime(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
