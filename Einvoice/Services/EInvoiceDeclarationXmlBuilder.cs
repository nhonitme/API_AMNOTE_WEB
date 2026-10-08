using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoiceDeclarationXmlBuilder
    {
        public static string Build(EInvoiceDeclarationInfo declaration)
        {
            var ctsDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "CTS")
                .OrderBy(x => x.STT)
                .Select(BuildCertificateDetail)
                .ToList();
            var tcgpDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "TCGP")
                .OrderBy(x => x.STT)
                .Select(BuildSolutionProviderDetail)
                .ToList();
            var tctnDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "TCTN")
                .OrderBy(x => x.STT)
                .Select(BuildTransferProviderDetail)
                .ToList();
            var dvhtptDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "DVHTPT")
                .OrderBy(x => x.STT)
                .Select(BuildDependentUnitDetail)
                .ToList();
            var dvduqDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "DVDUQTCUU")
                .OrderBy(x => x.STT)
                .Select(BuildAuthorizedLookupDetail)
                .ToList();
            var tnsdungDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "TNSDUNG")
                .OrderBy(x => x.STT)
                .Select(BuildTemporaryStopDetail)
                .ToList();
            var dkthDetails = declaration.DETAILS
                .Where(x => x.ISDEL != 1 && x.DETAIL_TYPE == "DKTH")
                .OrderBy(x => x.STT)
                .Select(BuildIntegrationDetail)
                .ToList();

            var ndTKhaiChildren = new List<XElement>
            {
                new XElement("HTHDon",
                    Number("CMa", declaration.CMA),
                    Number("CMTMTTien", declaration.CMTMTTIEN),
                    Number("KCMTMTTien", declaration.KCMTMTTIEN),
                    Number("KCMa", declaration.KCMA)
                ),
                new XElement("HTGDLHDDT",
                    Number("NNTDBKKhan", declaration.NNTDBKKHAN),
                    Number("CQXLTSCong", declaration.CQXLTSCONG),
                    Number("CDLTTDCQT", declaration.CDLTTDCQT),
                    Number("CDLQTCTN", declaration.CDLQTCTN),
                    Number("TCNNgoai", declaration.TCNNGOAI)
                ),
                new XElement("PThuc",
                    Number("CDDu", declaration.CDDU),
                    Number("CDLTHDThu", declaration.CDLTHDTHU),
                    Number("CBTHop", declaration.CBTHOP),
                    Number("CTTCTGDich", declaration.CTTCTGDICH)
                ),
                new XElement("LHDSDung",
                    Number("HDGTGT", declaration.HDGTGT),
                    Number("HDGTGTTHBLai", declaration.HDGTGTTHBLAI),
                    Number("HDBHang", declaration.HDBHANG),
                    Number("HDBHTHBLai", declaration.HDBHTHBLAI),
                    Number("HDTMai", declaration.HDTMAI),
                    Number("HDNCCNNgoai", declaration.HDNCCNNGOAI),
                    Number("HDBTSCong", declaration.HDBTSCONG),
                    Number("HDBHDTQGia", declaration.HDBHDTQGIA),
                    Number("HDKhac", declaration.HDKHAC),
                    Number("CTu", declaration.CTU)
                ),
                new XElement("DSCTSSDung", ctsDetails),
                new XElement("TTTCGP", tcgpDetails),
                new XElement("TTTCTN", tctnDetails),
            };

            if (dvhtptDetails.Count > 0)
                ndTKhaiChildren.Add(new XElement("TTDVHTPT", dvhtptDetails));
            if (dvduqDetails.Count > 0)
                ndTKhaiChildren.Add(new XElement("TTDVDUQTCuu", dvduqDetails));
            if (tnsdungDetails.Count > 0)
                ndTKhaiChildren.Add(new XElement("TTTNSDung", tnsdungDetails));
            if (dkthDetails.Count > 0)
                ndTKhaiChildren.Add(new XElement("TTDKTH", dkthDetails));

            var dlTKhai = new XElement("DLTKhai",
                    BuildGeneralInfo(declaration),
                    new XElement("NDTKhai", ndTKhaiChildren)
                );

            var dlTKhaiId = Common.NormalizeNullableText(declaration.MTDIEP);
            if (HasText(dlTKhaiId))
                dlTKhai.SetAttributeValue("Id", dlTKhaiId);

            var root = new XElement("TKhai",
                dlTKhai,
                new XElement("DSCKS",
                    new XElement("NNT",
                        new XElement("Signature")
                    ),
                    new XElement("CCKSKhac",
                        new XElement("Signature")
                    )
                )
            );

            var document = new XDocument(root);
            return XmlSerializationHelper.Serialize(document);
        }

        private static XElement BuildGeneralInfo(EInvoiceDeclarationInfo declaration)
        {
            return new XElement("TTChung",
                Text("PBan", declaration.PBAN),
                Text("MSo", declaration.MSO),
                Text("Ten", declaration.TEN),
                Number("HThuc", declaration.HTHUC),
                Text("TNNT", declaration.TNNT),
                Text("MST", declaration.MST),
                Text("CQTQLy", declaration.CQTQLY),
                Text("MCQTQLy", declaration.MCQTQLY),
                Text("TNDDPLuat", declaration.TNDDPLUAT),
                Text("DTDDPLuat", declaration.DTDDPLUAT),
                Text("CCCDan", declaration.CCCDAN),
                Text("SHChieu", declaration.SHCHIEU),
                Text("MQTNDDPLuat", declaration.MQTNDDPLUAT),
                Text("QTDDPLuat", declaration.QTICH),
                Date("NSDDPLuat", declaration.NSDDPLUAT),
                Text("DCLHe", declaration.DCLHE),
                Text("DCTDTu", declaration.DCTDTU),
                Text("NLHe", declaration.NLHE),
                Text("DTLHe", declaration.DTLHE),
                Text("DDanh", declaration.DDANH),
                Date("NLap", declaration.NLAP)
            );
        }

        private static XElement BuildCertificateDetail(EInvoiceDeclarationDetail detail)
        {
            return new XElement("CTS",
                Number("STT", detail.STT),
                Text("TTChuc", detail.TTCHUC),
                Text("Seri", detail.SERI),
                CertificateDateTime("TNgay", detail.TNGAY, endOfDay: false),
                CertificateDateTime("DNgay", detail.DNGAY, endOfDay: true),
                Number("HThuc", detail.CTS_HTHUC)
            );
        }

        private static XElement BuildSolutionProviderDetail(EInvoiceDeclarationDetail detail)
        {
            return new XElement("TCGP",
                Number("STT", detail.STT),
                Text("TTCGP", detail.TTCGP),
                Text("MSTTCGP", detail.MSTTCGP),
                Date("TNgay", detail.TNGAY),
                Date("DNgay", detail.DNGAY),
                Text("GChu", detail.GCHU)
            );
        }

        private static XElement BuildTransferProviderDetail(EInvoiceDeclarationDetail detail)
        {
            var elements = new List<XElement>
            {
                Number("STT", detail.STT),
                Text("TTCTN", detail.TTCTN),
                Text("MSTTCTN", detail.MSTTCTN),
                Date("TNgay", detail.TNGAY),
            };

            if (detail.DNGAY.HasValue)
                elements.Add(Date("DNgay", detail.DNGAY));

            if (HasText(detail.GCHU))
                elements.Add(Text("GChu", detail.GCHU));

            return new XElement("TCTN", elements);
        }

        private static XElement BuildDependentUnitDetail(EInvoiceDeclarationDetail detail)
        {
            var elements = new List<XElement>
            {
                Number("STT", detail.STT),
                Text("TDVHTPT", detail.TDVHTPT),
                Text("MSTDVHTPT", detail.MSTDVHTPT),
                Date("TNgay", detail.TNGAY),
            };

            if (detail.DNGAY.HasValue)
                elements.Add(Date("DNgay", detail.DNGAY));

            if (HasText(detail.GCHU))
                elements.Add(Text("GChu", detail.GCHU));

            return new XElement("DVHTPT", elements);
        }

        private static XElement BuildAuthorizedLookupDetail(EInvoiceDeclarationDetail detail)
        {
            var elements = new List<XElement>
            {
                Number("STT", detail.STT),
                Text("TDVi", detail.TDVI),
                Text("MST", detail.MSTDUQ),
                Number("HDBRMVao", detail.HDBRMVAO),
                Date("TDLHDTNgay", detail.TDLHDTNGAY),
                Date("TDLHDDNgay", detail.TDLHDDNGAY),
                Date("TGUQTNgay", detail.TGUQTNGAY),
                Date("TGUQDNgay", detail.TGUQDNGAY),
            };

            if (HasText(detail.GCHU))
                elements.Add(Text("GChu", detail.GCHU));

            return new XElement("DVDUQTCuu", elements);
        }

        private static XElement BuildTemporaryStopDetail(EInvoiceDeclarationDetail detail)
        {
            var elements = new List<XElement>
            {
                Number("STT", detail.STT),
                Date("TNgay", detail.TNGAY),
                Date("DNgay", detail.DNGAY),
                Text("TTCGP", detail.TTCGP),
                Text("MSTTCGP", detail.MSTTCGP),
            };

            if (HasText(detail.SERI))
                elements.Add(Text("Seri", detail.SERI));

            if (HasText(detail.GCHU))
                elements.Add(Text("GChu", detail.GCHU));

            return new XElement("TNSDung", elements);
        }

        private static XElement BuildIntegrationDetail(EInvoiceDeclarationDetail detail)
        {
            var elements = new List<XElement>
            {
                Number("STT", detail.STT),
                Text("TLHDon", detail.TLHDON),
                Number("KHMSHDon", detail.KHMSHDON),
                Text("KHHDon", detail.KHHDON),
                Text("Ten", detail.TENDKTH),
                Text("MST", detail.MSTDKTH),
                Text("MDich", detail.MDICH),
                Date("TNgay", detail.TNGAY),
                Date("DNgay", detail.DNGAY),
            };

            if (HasText(detail.GCHU))
                elements.Add(Text("GChu", detail.GCHU));

            return new XElement("DKTH", elements);
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

        private static XElement CertificateDateTime(string name, DateTime? value, bool endOfDay)
        {
            if (!value.HasValue)
                return new XElement(name, string.Empty);

            var dateTime = value.Value;
            if (dateTime.TimeOfDay == TimeSpan.Zero)
            {
                dateTime = endOfDay
                    ? dateTime.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                    : dateTime.Date;
            }

            return new XElement(name, dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss"));
        }

        private static bool HasText(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
