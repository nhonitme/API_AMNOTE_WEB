using System.Globalization;
using System.Xml.Linq;
using static API_AMNOTE_WEB.TaxWithholding.PitXmlElements;
namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitCertificateXmlBuilder
{
    public static string Build(PitCertificateDto data, long id, int version, long? number)
    {
        PitCertificateValidator.Validate(data);
        var payload = new XElement("DLCTu", new XAttribute("Id", $"PIT-{id}-v{version}"),
            new XElement("TTChung",
                new XElement("PBan", "2.1.1"),
                new XElement("TCTu", "Chứng từ khấu trừ thuế thu nhập cá nhân"),
                new XElement("MSCTu", "03/TNCN"),
                Value("KHCTu", data.KHCTU),
                new XElement("SCTu", number?.ToString(CultureInfo.InvariantCulture) ?? "0"),
                Value("NLap", data.NLAP),
                data.TCCTU.HasValue ? new XElement("TTCTLQuan",
                    Value("TCCTu", data.TCCTU),
                    Value("LHCTLQuan", data.LHCTLQUAN),
                    Value("KHMSCTCLQuan", data.KHMSCTCLQUAN),
                    Value("KHCTCLQuan", data.KHCTCLQUAN),
                    Value("SCTCLQuan", data.SCTCLQUAN),
                    Value("NLCTCLQuan", data.NLCTCLQUAN),
                    Value("GChu", data.GCHU)) : null),
            new XElement("NDCTu",
                new XElement("TCTTNhap",
                    Value("Ten", data.TCTTNHAP_TEN),
                    Value("MST", data.TCTTNHAP_MST),
                    Value("DChi", data.TCTTNHAP_DCHI),
                    Value("SDThoai", data.TCTTNHAP_SDTHOAI)),
                new XElement("NNT",
                    Value("Ten", data.NNT_TEN),
                    Value("MST", data.NNT_MST),
                    Value("DChi", data.NNT_DCHI),
                    Value("QTich", data.NNT_QTICH),
                    Value("CNCTru", data.NNT_CNCTRU),
                    Value("CCCDan", data.NNT_CCCDAN),
                    Value("SDThoai", data.NNT_SDTHOAI),
                    Value("DCTDTu", data.NNT_DCTDTU),
                    Value("GChu", data.NNT_GCHU)),
                new XElement("TTNCNKTru",
                    Value("KTNhap", data.KTNHAP),
                    Value("TThang", data.TTHANG),
                    Value("DThang", data.DTHANG),
                    Value("Nam", data.NAM),
                    Value("BHiem", data.BHIEM),
                    Value("TThien", data.TTHIEN),
                    Value("TTNCThue", data.TTNCTHUE),
                    Value("TTNTThue", data.TTNTTHUE),
                    Value("SThue", data.STHUE))));

        return Serialize("CTu", payload, "TCTTNhap");
    }
}
