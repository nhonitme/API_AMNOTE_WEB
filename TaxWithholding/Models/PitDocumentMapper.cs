namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitDocumentMapper
{
    private static T Common<T>(T result, PitStorageRow row, long id, string kind, DateTime? date, string? tax, string? name, string? series = null, long? number = null, long? xsl = null) where T : PitDocument
    {
        result.DOCUMENT_ID = id;
        result.KIND = kind;
        result.DOC_VERSION = row.DOC_VERSION;
        result.DOC_DATE = date;
        result.TAX_CD = tax ?? "";
        result.DISPLAY_NAME = name ?? "";
        result.SERIES = series;
        result.DOC_NO = number;
        result.XSL_ID = xsl;
        result.IS_SIGNED = row.IS_SIGNED;
        result.CQT_STATUS = row.CQT_STATUS;
        result.ERROR_MESSAGE = row.ERROR_MESSAGE;
        result.MTDIEP = row.MTDIEP;
        result.MGDDTU = row.MGDDTU;
        result.RAW_XML = row.RAW_XML;
        result.SIGNED_XML = row.SIGNED_XML;
        result.RESPONSE_XML = row.RESPONSE_XML;
        result.QUEUED = row.QUEUED;
        result.UPDATE_BY = row.UPDATE_BY;
        result.UPDATE_AT = row.UPDATE_AT;
        return result;
    }
    public static PitCertificateDto MapCertificate(PitCertificate x)
        => Common(new PitCertificateDto
        {
            KHCTU = x.KHCTU ?? "",
            NLAP = x.NLAP,
            TCCTU = x.TCCTU,
            LHCTLQUAN = x.LHCTLQUAN,
            KHMSCTCLQUAN = x.KHMSCTCLQUAN ?? "",
            KHCTCLQUAN = x.KHCTCLQUAN ?? "",
            SCTCLQUAN = x.SCTCLQUAN ?? "",
            NLCTCLQUAN = x.NLCTCLQUAN,
            GCHU = x.GCHU ?? "",
            TCTTNHAP_TEN = x.TCTTNHAP_TEN ?? "",
            TCTTNHAP_MST = x.TCTTNHAP_MST ?? "",
            TCTTNHAP_DCHI = x.TCTTNHAP_DCHI ?? "",
            TCTTNHAP_SDTHOAI = x.TCTTNHAP_SDTHOAI ?? "",
            NNT_TEN = x.NNT_TEN ?? "",
            NNT_MST = x.NNT_MST ?? "",
            NNT_DCHI = x.NNT_DCHI ?? "",
            NNT_QTICH = x.NNT_QTICH ?? "",
            NNT_CNCTRU = x.NNT_CNCTRU,
            NNT_CCCDAN = x.NNT_CCCDAN ?? "",
            NNT_SDTHOAI = x.NNT_SDTHOAI ?? "",
            NNT_DCTDTU = x.NNT_DCTDTU ?? "",
            NNT_GCHU = x.NNT_GCHU ?? "",
            KTNHAP = x.KTNHAP,
            TTHANG = x.TTHANG,
            DTHANG = x.DTHANG,
            NAM = x.NAM,
            BHIEM = x.BHIEM,
            TTHIEN = x.TTHIEN,
            TTNCTHUE = x.TTNCTHUE,
            TTNTTHUE = x.TTNTTHUE,
            STHUE = x.STHUE,
        }, x, x.CTU_ID, PitKinds.Certificate, x.NLAP, x.TCTTNHAP_MST, x.NNT_TEN, x.KHCTU, x.SCTU, x.XSL_ID);

    public static PitDeclarationDto MapDeclaration(PitDeclaration x)
        => Common(new PitDeclarationDto
        {
            HTHUC = x.HTHUC,
            TNNT = x.TNNT ?? "",
            MST = x.MST ?? "",
            CQTQLY = x.CQTQLY ?? "",
            MCQTQLY = x.MCQTQLY ?? "",
            NLHE = x.NLHE ?? "",
            DCLHE = x.DCLHE ?? "",
            DCTDTU = x.DCTDTU ?? "",
            DTLHE = x.DTLHE ?? "",
            DDANH = x.DDANH ?? "",
            NLAP = x.NLAP,
        }, x, x.TKHAI_ID, PitKinds.Declaration, x.NLAP, x.MST, x.TNNT);

    public static PitErrorNoticeDto MapErrorNotice(PitErrorNotice x)
        => Common(new PitErrorNoticeDto
        {
            LOAI = x.LOAI,
            SO = x.SO ?? "",
            NTBCCQT = x.NTBCCQT,
            MCQT = x.MCQT ?? "",
            TCQT = x.TCQT ?? "",
            TNNT = x.TNNT ?? "",
            MST = x.MST ?? "",
            MDVQHNSACH = x.MDVQHNSACH ?? "",
            DDANH = x.DDANH ?? "",
            NTBAO = x.NTBAO,
        }, x, x.TBAO_ID, PitKinds.ErrorNotice, x.NTBAO, x.MST ?? "", x.TNNT);

}
