using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitService(
    PitRepository repository,
    PitXslService xslTemplates,
    PitMessageOutboundService transmission,
    ICompanyDatabaseResolver databases,
    IEInvoiceMessageRepository messages,
    IEInvoiceTemplateRepository einvoiceTemplates,
    IEInvoiceHtmlToPdfService htmlToPdfService,
    ILogger<PitService> logger)
{
    private const string DeclarationPrintTemplateCode = "01/ĐKTĐ-CTĐT";

    public Task<(List<PitDocument> Items, int TotalRecords)> Search(
        string company,
        string kind,
        DateTime? from,
        DateTime? to,
        string? keyword,
        int? signed,
        int? cqtStatus,
        int pageNumber,
        int pageSize)
    {
        if(from>to)throw new ArgumentException("Khoảng ngày không hợp lệ.");
        if(pageNumber<0||pageSize<0||pageSize>500)throw new ArgumentException("Phân trang không hợp lệ.");
        return repository.Search(company,kind,from:from,to:to,keyword:keyword,signed:signed,cqtStatus:cqtStatus,pageNumber:pageNumber,pageSize:pageSize);
    }
    public Task<PitDocument> Get(string company,string kind,long id)=>repository.Get(company,kind,id);
    public async Task<PitDocument> Save(string company,string user,string kind,long id,PitSaveRequest request)
    {
        PitXmlBuilder.Validate(kind,request.DATA);
        if(kind==PitKinds.Certificate)
        {
            if(request.XSL_ID is not > 0)
                throw new ArgumentException("Chọn mẫu số / ký hiệu chứng từ từ danh mục.");
            var catalog=await xslTemplates.RequireActiveCatalogAsync(company,request.XSL_ID.Value);
            var series=request.DATA.Get("KHCTU").ToUpperInvariant();
            if(!string.Equals(series,catalog.SERIES,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Ký hiệu không khớp mẫu số đã chọn trong danh mục.");
            request.DATA.Fields["KHCTU"]=catalog.SERIES!;
        }
        if(kind==PitKinds.ErrorNotice)
        foreach(var item in request.DATA.Items.Where(i=>i.REF_CTU_ID>0))
        {
            var certificate=await repository.Get(company,PitKinds.Certificate,item.REF_CTU_ID!.Value);
            if(certificate.IS_SIGNED!=1 || certificate.TAX_CD!=request.DATA.Get("MST") || item.KHCTU!=certificate.SERIES || item.SCTU!=certificate.DOC_NO?.ToString() || item.NLAP!=certificate.DOC_DATE?.ToString("yyyy-MM-dd") || item.KHMSCTU!="03/TNCN"||item.LCTDT!="8")
                throw new ArgumentException("Chứng từ được chọn không khớp thông tin đã ký hoặc MST tổ chức.");
        }
        var savedId=await repository.Save(company,user,kind,id,request);
        return await repository.Get(company,kind,savedId);
    }
    public Task Delete(string company,string user,string kind,long id,int version)=>repository.Delete(company,user,kind,id,version);
    public async Task<PitSigningPayload> Prepare(string company,string kind,long id)
    {
        if(kind==PitKinds.Certificate)
            await RequireActiveCertificateTemplate(company,id);
        var row=await repository.Prepare(company,kind,id);
        return new(row.DOC_VERSION,row.RAW_XML!,"PIT");
    }
    public async Task<PitDocument> Sign(string company,string user,string kind,long id,PitSignRequest request)
    {
        if(kind==PitKinds.Certificate)
            await RequireActiveCertificateTemplate(company,id);
        await repository.Sign(company,user,kind,id,request);
        try { await transmission.DispatchAsync(await databases.ResolveDatabaseNameAsync(company),company); }
        catch(Exception ex){logger.LogWarning(ex,"PIT document {Id} signed; outbox retry pending",id);}
        return await repository.Get(company,kind,id);
    }
    public async Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> History(string company,string kind,long id)
    {
        var row=await repository.Get(company,kind,id);
        return string.IsNullOrWhiteSpace(row.MTDIEP)?Array.Empty<EInvoiceMessageReceiveInfo>():await messages.GetReceiveMessagesByLookupCodeAsync(company,row.MTDIEP);
    }
    public async Task<string> Xml(string company,string kind,long id)
    {
        var row=await repository.Get(company,kind,id);return row.SIGNED_XML??PitXmlBuilder.Build(row);
    }
    public async Task<string> Print(string company,string kind,long id)
    {
        var row=await repository.Get(company,kind,id);
        var xml=row.SIGNED_XML??PitXmlBuilder.Build(row);
        if(kind==PitKinds.Declaration)
        {
            var declarationTemplate=await einvoiceTemplates.GetActiveTemplateAsync("XSL",DeclarationPrintTemplateCode)
                ?? throw new InvalidOperationException($"Chưa cấu hình mẫu in {DeclarationPrintTemplateCode}.");
            if(string.IsNullOrWhiteSpace(declarationTemplate.CONTENT))
                throw new InvalidOperationException($"Mẫu in {DeclarationPrintTemplateCode} chưa có nội dung XSL.");
            return EInvoiceXmlHtmlTransformService.Transform(xml,declarationTemplate.CONTENT,lookupCompanyCd:company);
        }
        if(kind!=PitKinds.Certificate)
            throw new InvalidOperationException("Loại chứng từ này chưa hỗ trợ mẫu in XSL.");
        var certificateTemplate=await xslTemplates.ResolveForPrintAsync(company,row.XSL_ID);
        return xslTemplates.Transform(company,xml,certificateTemplate);
    }

    public async Task<byte[]> PrintPdf(
        string company,
        string kind,
        long id,
        CancellationToken cancellationToken = default)
    {
        var html=await Print(company,kind,id);
        return await htmlToPdfService.ConvertAsync(html,cancellationToken);
    }

    private async Task RequireActiveCertificateTemplate(string company,long id)
    {
        var document=await repository.Get(company,PitKinds.Certificate,id);
        if(document.XSL_ID is not > 0)
            throw new ArgumentException("Chọn mẫu số / ký hiệu chứng từ từ danh mục.");
        await xslTemplates.RequireActiveCatalogAsync(company,document.XSL_ID.Value);
    }
}
