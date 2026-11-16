namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitXmlBuilder
{
    public static string Build(PitDocument document) => document switch
    {
        PitCertificateDto data => PitCertificateXmlBuilder.Build(data, data.DOCUMENT_ID, data.DOC_VERSION, data.DOC_NO),
        PitDeclarationDto data => PitDeclarationXmlBuilder.Build(data, data.DOCUMENT_ID, data.DOC_VERSION),
        PitErrorNoticeDto data => PitErrorNoticeXmlBuilder.Build(data, data.DOCUMENT_ID, data.DOC_VERSION),
        _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
    };
}
