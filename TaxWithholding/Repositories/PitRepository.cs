using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Services;
using System.Globalization;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitRepository(DapperExecutor db)
{
    private sealed class CountRow { public int TOTAL_RECORDS { get; set; } }
    private sealed class DocumentNoRow { public long SCTU { get; set; } }

    public async Task<(List<PitDocument> Items, int TotalRecords)> Search(
        string company,
        string kind,
        long id=0,
        DateTime? from=null,
        DateTime? to=null,
        string? keyword=null,
        int? signed=null,
        int? cqtStatus=null,
        int pageNumber=0,
        int pageSize=0,
        string? dbName=null)
    {
        _=PitKinds.Message(kind);
        var offset=pageNumber>0&&pageSize>0?(pageNumber-1)*pageSize:0;
        var parameters=new
        {
            company,
            id,
            from=API_AMNOTE_WEB.Helpers.Common.FormatNullableYmd(from),
            to=API_AMNOTE_WEB.Helpers.Common.FormatNullableYmd(to),
            keyword,
            signed,
            cqtStatus,
            offset,
            pageSize
        };
        return kind switch
        {
            PitKinds.Declaration => MapResult(
                await db.QueryMultipleAsync<PitDeclaration,CountRow>(Net_DB.Net_DB_Company,
                    "CALL getPitTkhai(@company,@id,@from,@to,@keyword,@signed,@cqtStatus,@offset,@pageSize)",parameters,sDBName:dbName),
                MapDeclaration),
            PitKinds.Certificate => MapResult(
                await db.QueryMultipleAsync<PitCertificate,CountRow>(Net_DB.Net_DB_Company,
                    "CALL getPitCtu(@company,@id,@from,@to,@keyword,@signed,@cqtStatus,@offset,@pageSize)",parameters,sDBName:dbName),
                MapCertificate),
            PitKinds.ErrorNotice => MapResult(
                await db.QueryMultipleAsync<PitErrorNotice,CountRow>(Net_DB.Net_DB_Company,
                    "CALL getPitTbao(@company,@id,@from,@to,@keyword,@signed,@cqtStatus,@offset,@pageSize)",parameters,sDBName:dbName),
                MapErrorNotice),
            _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
        };
    }
    public async Task<PitDocument> Get(string company,string kind,long id,string? dbName=null)
    {
        var row=(await Search(company,kind,id,dbName:dbName)).Items.SingleOrDefault()??throw new KeyNotFoundException("Không tìm thấy chứng từ.");
        if(kind==PitKinds.Declaration)
        {
            var details=await db.QueryAsync<PitDeclarationCertificate>(Net_DB.Net_DB_Company,
                "CALL getPitTkhaiCts(@company,@id)",new{company,id},sDBName:dbName);
            row.DATA.Certificates=details.Select(x=>new PitCertificateRegistration{
                TTCHUC=x.TTCHUC,SERI=x.SERI,TNGAY=XmlDate(x.TNGAY),DNGAY=XmlDate(x.DNGAY),HTHUC=x.HTHUC}).ToList();
        }
        else if(kind==PitKinds.ErrorNotice)
        {
            var details=await db.QueryAsync<PitErrorNoticeCertificate>(Net_DB.Net_DB_Company,
                "CALL getPitTbaoCtu(@company,@id)",new{company,id},sDBName:dbName);
            row.DATA.Items=details.Select(x=>new PitNoticeItem{
                REF_CTU_ID=x.REF_CTU_ID,KHMSCTU=x.KHMSCTU,KHCTU=x.KHCTU,SCTU=x.SCTU,
                NLAP=x.NLAP.ToString("yyyy-MM-dd"),LCTDT=x.LCTDT,LDO=x.LDO??""}).ToList();
        }
        return row;
    }
    public async Task<long> Save(string company,string user,string kind,long id,PitSaveRequest request)
    {
        var data=request.DATA;
        await using var session=await db.CreateSessionAsync();
        var result=kind switch
        {
            PitKinds.Declaration => await SaveDeclaration(session,company,user,id,request),
            PitKinds.Certificate => await SaveCertificate(session,company,user,id,request),
            PitKinds.ErrorNotice => await SaveErrorNotice(session,company,user,id,request),
            _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
        };
        session.Commit();return result;
    }
    public async Task Delete(string company,string user,string kind,long id,int version)
    {
        await using var session=await db.CreateSessionAsync();
        var proc=kind switch{PitKinds.Declaration=>"delPitTkhai",PitKinds.Certificate=>"delPitCtu",PitKinds.ErrorNotice=>"delPitTbao",_=>throw new ArgumentException("Loại chứng từ không hợp lệ.")};
        await session.ExecuteAsync($"CALL {proc}(@company,@id,@version,@user)",new{company,id,version,user});session.Commit();
    }
    public async Task<PitDocument> Prepare(string company,string kind,long id)
    {
        await using var session=await db.CreateSessionAsync();
        var target=PitKinds.Target(kind);
        var row=await LockAndMap(session,"lockPitTarget",company,kind,id);
        if(kind==PitKinds.Certificate)
        {
            var signingDate=SigningDate();
            row.DOC_DATE=signingDate;
            row.DATA.Fields["NLAP"]=Date(signingDate);
            row.DOC_NO=(await session.QuerySingleAsync<DocumentNoRow>(
                "CALL getNextPitCtuNo(@company,@id)",new{company,id})).SCTU;
        }
        if(kind!=PitKinds.Declaration)
        {
            var accepted=await session.QuerySingleAsync<int>("CALL getPitRegistration(@company,@tax)",new{company,tax=row.TAX_CD});
            if(accepted==0) throw new InvalidOperationException("Cần tờ khai đăng ký chứng từ được CQT chấp nhận (111) trước khi ký gửi.");
        }
        row.RAW_XML=PitXmlBuilder.Build(row);
        row.MTDIEP ??= EInvoiceMessageXmlBuilder.GenerateMessageCode();
        await session.ExecuteAsync("CALL setPitSigningXml(@company,@target,@id,@xml,@message)",new{company,target,id,xml=row.RAW_XML,message=row.MTDIEP});
        session.Commit();return row;
    }
    public async Task Sign(string company,string user,string kind,long id,PitSignRequest request)
    {
        await using var session=await db.CreateSessionAsync();
        var target=PitKinds.Target(kind);
        var row=await LockAndMap(session,"lockPitTarget",company,kind,id);
        if(row.IS_SIGNED==1) { if(row.SIGNED_XML==request.XML) {session.Commit();return;} throw new InvalidOperationException("Chứng từ đã ký."); }
        if(row.DOC_VERSION!=request.DOC_VERSION||string.IsNullOrWhiteSpace(row.RAW_XML))throw new InvalidOperationException("Dữ liệu ký đã thay đổi. Tải lại và ký lại.");
        var certificateSerial=TaxDocumentSignatureValidator.Validate(row.RAW_XML,request.XML,PitSchemas.Get(kind).Signer);
        if(kind!=PitKinds.Declaration)
        {
            var approved=await session.QuerySingleAsync<int>(
                "CALL isApprovedSigningCertificate(@company,'PIT',@tax,@serial)",new{company,tax=row.TAX_CD,serial=certificateSerial});
            if(approved!=1)
                throw new InvalidOperationException("Chứng thư số chưa được CQT chấp nhận, đã ngừng sử dụng hoặc hết hiệu lực.");
        }
        if(kind==PitKinds.Certificate)
        {
            var next=(await session.QuerySingleAsync<DocumentNoRow>(
                "CALL getNextPitCtuNo(@company,@id)",new{company,id})).SCTU;
            if(ReadCertificateNumber(row.RAW_XML)!=next)
                throw new InvalidOperationException("Số chứng từ đã thay đổi. Vui lòng tải lại và ký lại.");
            var signingDate=ReadCertificateDate(row.RAW_XML);
            if(signingDate!=SigningDate())
                throw new InvalidOperationException("Ngày ký đã thay đổi. Vui lòng ký lại.");
            await session.ExecuteAsync(
                "CALL setPitCtuSigningInfo(@company,@id,@docNo,@date)",new{company,id,docNo=next,date=signingDate});
        }
        await session.ExecuteAsync("CALL setPitSignature(@company,@target,@id,@version,@xml,@user)",new{company,target,id,version=request.DOC_VERSION,xml=request.XML,user});
        session.Commit();
    }

    private static (List<PitDocument> Items,int TotalRecords) MapResult<T>(
        (IReadOnlyList<T> First,IReadOnlyList<CountRow> Second) result,Func<T,PitDocument> map)
        =>(result.First.Select(map).ToList(),result.Second.FirstOrDefault()?.TOTAL_RECORDS??0);

    private static PitDocument Common(PitStorageRow row,long id,string kind,DateTime? date,string tax,string name,string? series=null,long? number=null,long? xsl=null)
        =>new(){DOCUMENT_ID=id,KIND=kind,DOC_VERSION=row.DOC_VERSION,DOC_DATE=date,TAX_CD=tax,DISPLAY_NAME=name,
            SERIES=series,DOC_NO=number,XSL_ID=xsl,IS_SIGNED=row.IS_SIGNED,CQT_STATUS=row.CQT_STATUS,
            ERROR_MESSAGE=row.ERROR_MESSAGE,MTDIEP=row.MTDIEP,MGDDTU=row.MGDDTU,RAW_XML=row.RAW_XML,
            SIGNED_XML=row.SIGNED_XML,RESPONSE_XML=row.RESPONSE_XML,QUEUED=row.QUEUED,UPDATE_BY=row.UPDATE_BY,UPDATE_AT=row.UPDATE_AT};

    private static PitDocument MapDeclaration(PitDeclaration x)
    {
        var row=Common(x,x.TKHAI_ID,PitKinds.Declaration,x.NLAP,x.MST,x.TNNT);
        row.DATA.Fields=MapFields(x,PitKinds.Declaration);
        return row;
    }
    private static PitDocument MapCertificate(PitCertificate x)
    {
        var row=Common(x,x.CTU_ID,PitKinds.Certificate,x.NLAP,x.TCTTNHAP_MST,x.NNT_TEN,x.KHCTU,x.SCTU,x.XSL_ID);
        row.DATA.Fields=MapFields(x,PitKinds.Certificate);
        return row;
    }
    private static PitDocument MapErrorNotice(PitErrorNotice x)
    {
        var row=Common(x,x.TBAO_ID,PitKinds.ErrorNotice,x.NTBAO,x.MST??"",x.TNNT);
        row.DATA.Fields=MapFields(x,PitKinds.ErrorNotice);
        return row;
    }

    private static Dictionary<string,string> MapFields(PitStorageRow source,string kind)
    {
        var type=source.GetType();
        string Read(PitField field)
        {
            var property=type.GetProperty(field.Key)
                ??throw new InvalidOperationException($"Cột {field.Key} chưa có trong {type.Name}.");
            return FieldText(property.GetValue(source));
        }
        return PitSchemas.Get(kind).Fields.ToDictionary(field=>field.Key,Read);
    }

    private static string FieldText(object? value)=>value switch
    {
        null=>"",
        DateTime date=>Date(date),
        decimal amount=>Money(amount),
        IFormattable formattable=>formattable.ToString(null,CultureInfo.InvariantCulture)??"",
        _=>value.ToString()??""
    };

    private static async Task<long> SaveDeclaration(DapperSession s,string company,string user,long id,PitSaveRequest r)
    {
        var d=r.DATA;var result=await s.QuerySingleAsync<long>("CALL setPitTkhai(@company,@id,@version,@method,@name,@tax,@office,@officeCd,@contact,@address,@email,@phone,@location,@date,@user)",
            new{company,id,version=r.DOC_VERSION,method=int.Parse(d.Get("HTHUC")),name=d.Get("TNNT"),tax=d.Get("MST"),office=d.Get("CQTQLY"),officeCd=d.Get("MCQTQLY"),contact=d.Get("NLHE"),address=d.Get("DCLHE"),email=d.Get("DCTDTU"),phone=d.Get("DTLHE"),location=d.Get("DDANH"),date=DateTime.Parse(d.Get("NLAP")),user});
        await s.ExecuteAsync("CALL delPitTkhaiCts(@company,@id)",new{company,id=result});
        for(var i=0;i<d.Certificates.Count;i++){var c=d.Certificates[i];await s.ExecuteAsync("CALL setPitTkhaiCts(@company,@id,@stt,@issuer,@serial,@from,@to,@method,@user)",
            new{company,id=result,stt=i+1,issuer=c.TTCHUC,serial=c.SERI,from=DateTimeOffset.Parse(c.TNGAY).ToOffset(TimeSpan.FromHours(7)).DateTime,to=DateTimeOffset.Parse(c.DNGAY).ToOffset(TimeSpan.FromHours(7)).DateTime,method=c.HTHUC,user});}
        return result;
    }
    private static Task<long> SaveCertificate(DapperSession s,string company,string user,long id,PitSaveRequest r)
    {
        var d=r.DATA;DateTime? OptionalDate(string key)=>DateTime.TryParse(d.Get(key),out var value)?value:null;
        int? OptionalInt(string key)=>int.TryParse(d.Get(key),out var value)?value:null;
        decimal Amount(string key)=>decimal.Parse(d.Get(key),System.Globalization.CultureInfo.InvariantCulture);
        return s.QuerySingleAsync<long>("CALL setPitCtu(@company,@id,@version,@xsl,@series,@date,@relatedKind,@relatedType,@relatedForm,@relatedSeries,@relatedNo,@relatedDate,@relatedNote,@payerName,@payerTax,@payerAddress,@payerPhone,@personName,@personTax,@personAddress,@nationality,@resident,@identity,@personPhone,@personEmail,@personNote,@incomeKind,@fromMonth,@toMonth,@year,@insurance,@charity,@taxable,@assessable,@withheld,@user)",
            new{company,id,version=r.DOC_VERSION,xsl=r.XSL_ID,series=d.Get("KHCTU"),date=OptionalDate("NLAP"),relatedKind=OptionalInt("TCCTU"),relatedType=OptionalInt("LHCTLQUAN"),relatedForm=d.Get("KHMSCTCLQUAN"),relatedSeries=d.Get("KHCTCLQUAN"),relatedNo=d.Get("SCTCLQUAN"),relatedDate=OptionalDate("NLCTCLQUAN"),relatedNote=d.Get("GCHU"),payerName=d.Get("TCTTNHAP_TEN"),payerTax=d.Get("TCTTNHAP_MST"),payerAddress=d.Get("TCTTNHAP_DCHI"),payerPhone=d.Get("TCTTNHAP_SDTHOAI"),personName=d.Get("NNT_TEN"),personTax=d.Get("NNT_MST"),personAddress=d.Get("NNT_DCHI"),nationality=d.Get("NNT_QTICH"),resident=int.Parse(d.Get("NNT_CNCTRU")),identity=d.Get("NNT_CCCDAN"),personPhone=d.Get("NNT_SDTHOAI"),personEmail=d.Get("NNT_DCTDTU"),personNote=d.Get("NNT_GCHU"),incomeKind=d.Get("KTNHAP"),fromMonth=int.Parse(d.Get("TTHANG")),toMonth=int.Parse(d.Get("DTHANG")),year=int.Parse(d.Get("NAM")),insurance=Amount("BHIEM"),charity=Amount("TTHIEN"),taxable=Amount("TTNCTHUE"),assessable=Amount("TTNTTHUE"),withheld=Amount("STHUE"),user});
    }
    private static async Task<long> SaveErrorNotice(DapperSession s,string company,string user,long id,PitSaveRequest r)
    {
        var d=r.DATA;var result=await s.QuerySingleAsync<long>("CALL setPitTbao(@company,@id,@version,@noticeKind,@noticeNo,@noticeDate,@officeCd,@office,@name,@tax,@budget,@location,@date,@user)",
            new{company,id,version=r.DOC_VERSION,noticeKind=int.Parse(d.Get("LOAI")),noticeNo=d.Get("SO"),noticeDate=DateTime.TryParse(d.Get("NTBCCQT"),out var nd)?nd:(DateTime?)null,officeCd=d.Get("MCQT"),office=d.Get("TCQT"),name=d.Get("TNNT"),tax=d.Get("MST"),budget=d.Get("MDVQHNSACH"),location=d.Get("DDANH"),date=DateTime.Parse(d.Get("NTBAO")),user});
        await s.ExecuteAsync("CALL delPitTbaoCtu(@company,@id)",new{company,id=result});
        for(var i=0;i<d.Items.Count;i++){var x=d.Items[i];await s.ExecuteAsync("CALL setPitTbaoCtu(@company,@id,@refId,@stt,@form,@series,@number,@date,@type,@reason,@user)",
            new{company,id=result,refId=x.REF_CTU_ID,stt=i+1,form=x.KHMSCTU,series=x.KHCTU,number=x.SCTU,date=DateTime.Parse(x.NLAP),type=x.LCTDT,reason=x.LDO,user});}
        return result;
    }
    private static async Task<PitDocument> LockAndMap(DapperSession s,string procedure,string company,string kind,long id)
    {
        var target=PitKinds.Target(kind);PitDocument row=kind switch
        {
            PitKinds.Declaration=>MapDeclaration(await s.QuerySingleAsync<PitDeclaration>($"CALL {procedure}(@company,@target,@id)",new{company,target,id})),
            PitKinds.Certificate=>MapCertificate(await s.QuerySingleAsync<PitCertificate>($"CALL {procedure}(@company,@target,@id)",new{company,target,id})),
            PitKinds.ErrorNotice=>MapErrorNotice(await s.QuerySingleAsync<PitErrorNotice>($"CALL {procedure}(@company,@target,@id)",new{company,target,id})),
            _=>throw new ArgumentException("Loại chứng từ không hợp lệ.")
        };
        if(kind==PitKinds.Declaration){var rows=await s.QueryAsync<PitDeclarationCertificate>("CALL getPitTkhaiCts(@company,@id)",new{company,id});row.DATA.Certificates=rows.Select(x=>new PitCertificateRegistration{TTCHUC=x.TTCHUC,SERI=x.SERI,TNGAY=XmlDate(x.TNGAY),DNGAY=XmlDate(x.DNGAY),HTHUC=x.HTHUC}).ToList();}
        if(kind==PitKinds.ErrorNotice){var rows=await s.QueryAsync<PitErrorNoticeCertificate>("CALL getPitTbaoCtu(@company,@id)",new{company,id});row.DATA.Items=rows.Select(x=>new PitNoticeItem{REF_CTU_ID=x.REF_CTU_ID,KHMSCTU=x.KHMSCTU,KHCTU=x.KHCTU,SCTU=x.SCTU,NLAP=Date(x.NLAP),LCTDT=x.LCTDT,LDO=x.LDO??""}).ToList();}
        return row;
    }
    private static string Date(DateTime value)=>value.ToString("yyyy-MM-dd");
    private static string Date(DateTime? value)=>value?.ToString("yyyy-MM-dd")??"";
    private static string XmlDate(DateTime value)=>new DateTimeOffset(DateTime.SpecifyKind(value,DateTimeKind.Unspecified),TimeSpan.FromHours(7)).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
    private static string Money(decimal value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
    private static DateTime SigningDate()=>DateTime.UtcNow.AddHours(7).Date;

    private static long ReadCertificateNumber(string xml)
    {
        try
        {
            var value=XDocument.Parse(xml)
                .Descendants()
                .FirstOrDefault(element=>element.Name.LocalName=="SCTu")
                ?.Value;
            return long.TryParse(value,out var number)&&number>0
                ? number
                : throw new InvalidOperationException();
        }
        catch
        {
            throw new InvalidOperationException("XML ký không có số chứng từ hợp lệ.");
        }
    }

    private static DateTime ReadCertificateDate(string xml)
    {
        try
        {
            var value=XDocument.Parse(xml)
                .Descendants()
                .FirstOrDefault(element=>element.Name.LocalName=="NLap")
                ?.Value;
            return DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)
                ? date
                : throw new InvalidOperationException();
        }
        catch
        {
            throw new InvalidOperationException("XML ký không có ngày lập hợp lệ.");
        }
    }
}
