using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Services;
using System.Globalization;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitRepository(DapperExecutor db,
    IPitDeclarationRepository declarations, IPitCertificateRepository certificates, IPitErrorNoticeRepository notices, ElectronicDocumentPdfService pdf) : IPitRepository
{
    private sealed class DocumentNoRow { public long SCTU { get; set; } }

    public async Task<(List<PitDocument> Items, int TotalRecords)> Search(
        string company,
        string kind,
        long id = 0,
        DateTime? from = null,
        DateTime? to = null,
        string? keyword = null,
        int? signed = null,
        int? cqtStatus = null,
        int pageNumber = 0,
        int pageSize = 0,
        string? dbName = null)
    {
        _ = PitKinds.Message(kind);
        var offset = pageNumber > 0 && pageSize > 0 ? (pageNumber - 1) * pageSize : 0;
        var parameters = new
        {
            company,
            id,
            from = API_AMNOTE_WEB.Helpers.Common.FormatNullableYmd(from),
            to = API_AMNOTE_WEB.Helpers.Common.FormatNullableYmd(to),
            keyword,
            signed,
            cqtStatus,
            offset,
            pageSize
        };
        return kind switch
        {
            PitKinds.Declaration => MapResult(
                await declarations.SearchAsync(parameters, dbName),
                PitDocumentMapper.MapDeclaration),
            PitKinds.Certificate => MapResult(
                await certificates.SearchAsync(parameters, dbName),
                PitDocumentMapper.MapCertificate),
            PitKinds.ErrorNotice => MapResult(
                await notices.SearchAsync(parameters, dbName),
                PitDocumentMapper.MapErrorNotice),
            _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
        };
    }
    public async Task<PitDocument> Get(string company, string kind, long id, string? dbName = null)
    {
        var row = (await Search(company, kind, id, dbName: dbName)).Items.SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy chứng từ.");
        if (kind == PitKinds.Declaration)
        {
            var details = await db.QueryAsync<PitDeclarationCertificate>(Net_DB.Net_DB_Company,
                "CALL getPitTkhaiCts(@company,@id)", new { company, id }, sDBName: dbName);
            ((PitDeclarationDto)row).Certificates = details.Select(x => new PitCertificateRegistration
            {
                TTCHUC = x.TTCHUC,
                SERI = x.SERI,
                TNGAY = XmlDate(x.TNGAY),
                DNGAY = XmlDate(x.DNGAY),
                HTHUC = x.HTHUC
            }).ToList();
        }
        else if (kind == PitKinds.ErrorNotice)
        {
            var details = await db.QueryAsync<PitErrorNoticeCertificate>(Net_DB.Net_DB_Company,
                "CALL getPitTbaoCtu(@company,@id)", new { company, id }, sDBName: dbName);
            ((PitErrorNoticeDto)row).Items = details.Select(x => new PitNoticeItem
            {
                REF_CTU_ID = x.REF_CTU_ID,
                KHMSCTU = x.KHMSCTU,
                KHCTU = x.KHCTU,
                SCTU = x.SCTU,
                NLAP = x.NLAP,
                LCTDT = x.LCTDT,
                LDO = x.LDO ?? ""
            }).ToList();
        }
        return row;
    }
    public async Task Delete(string company, string user, string kind, long id, int version)
    {
        await using var session = await db.CreateSessionAsync();
        var proc = kind switch { PitKinds.Declaration => "delPitTkhai", PitKinds.Certificate => "delPitCtu", PitKinds.ErrorNotice => "delPitTbao", _ => throw new ArgumentException("Loại chứng từ không hợp lệ.") };
        await session.ExecuteAsync($"CALL {proc}(@company,@id,@version,@user)", new { company, id, version, user }); session.Commit();
    }
    public async Task<PitDocument> Prepare(string company, string kind, long id)
    {
        await using var session = await db.CreateSessionAsync();
        var target = PitKinds.Target(kind);
        var row = await LockAndMap(session, "lockPitTarget", company, kind, id);
        if (kind == PitKinds.Certificate)
        {
            var signingDate = SigningDate();
            row.DOC_DATE = signingDate;
            ((PitCertificateDto)row).NLAP = signingDate;
            row.DOC_NO = (await session.QuerySingleAsync<DocumentNoRow>(
                "CALL getNextPitCtuNo(@company,@id)", new { company, id })).SCTU;
        }
        if (kind != PitKinds.Declaration)
        {
            var accepted = await session.QuerySingleAsync<int>("CALL getPitRegistration(@company,@tax)", new { company, tax = row.TAX_CD });
            if (accepted == 0) throw new InvalidOperationException("Cần tờ khai đăng ký chứng từ được CQT chấp nhận (111) trước khi ký gửi.");
        }
        row.RAW_XML = PitXmlBuilder.Build(row);
        row.MTDIEP ??= EInvoiceMessageXmlBuilder.GenerateMessageCode();
        await session.ExecuteAsync("CALL setPitSigningXml(@company,@target,@id,@xml,@message)", new { company, target, id, xml = row.RAW_XML, message = row.MTDIEP });
        session.Commit(); return row;
    }
    public async Task Sign(string company, string user, string kind, long id, PitSignRequest request)
    {
        await using var session = await db.CreateSessionAsync();
        var target = PitKinds.Target(kind);
        var row = await LockAndMap(session, "lockPitTarget", company, kind, id);
        if (row.IS_SIGNED == 1) { if (row.SIGNED_XML == request.XML) { session.Commit(); return; } throw new InvalidOperationException("Chứng từ đã ký."); }
        if (row.DOC_VERSION != request.DOC_VERSION || string.IsNullOrWhiteSpace(row.RAW_XML)) throw new InvalidOperationException("Dữ liệu ký đã thay đổi. Tải lại và ký lại.");
        if (kind == PitKinds.Declaration && XElement.Parse(row.RAW_XML).Element("DLTKhai")?.Element("NDTKhai")?.Element("LHSDung")?.Element("CTKTTTMDTu") != null)
            throw new InvalidOperationException("XML đăng ký cần cập nhật theo QĐ 1233. Vui lòng tạo lại dữ liệu ký.");
        var certificateSerial = TaxDocumentSignatureValidator.Validate(row.RAW_XML, request.XML, PitDocumentRules.Signer(kind));
        if (kind != PitKinds.Declaration)
        {
            var approved = await session.QuerySingleAsync<int>(
                "CALL isApprovedSigningCertificate(@company,'PIT',@tax,@serial)", new { company, tax = row.TAX_CD, serial = certificateSerial });
            if (approved != 1)
                throw new InvalidOperationException("Chứng thư số chưa được CQT chấp nhận, đã ngừng sử dụng hoặc hết hiệu lực.");
        }
        if (kind == PitKinds.Certificate)
        {
            var next = (await session.QuerySingleAsync<DocumentNoRow>(
                "CALL getNextPitCtuNo(@company,@id)", new { company, id })).SCTU;
            if (ReadCertificateNumber(row.RAW_XML) != next)
                throw new InvalidOperationException("Số chứng từ đã thay đổi. Vui lòng tải lại và ký lại.");
            var signingDate = ReadCertificateDate(row.RAW_XML);
            if (signingDate != SigningDate())
                throw new InvalidOperationException("Ngày ký đã thay đổi. Vui lòng ký lại.");
            await session.ExecuteAsync(
                "CALL setPitCtuSigningInfo(@company,@id,@docNo,@date)", new { company, id, docNo = next, date = signingDate });
        }
        await session.ExecuteAsync("CALL setPitSignature(@company,@target,@id,@version,@xml,@user)", new { company, target, id, version = request.DOC_VERSION, xml = request.XML, user });
        if (kind != PitKinds.Certificate) await pdf.PinSharedTemplateAsync(session, company, "pit-" + kind, id);
        session.Commit();
    }

    private static (List<PitDocument> Items, int TotalRecords) MapResult<T>(
        (IReadOnlyList<T> First, IReadOnlyList<PitCountRow> Second) result, Func<T, PitDocument> map)
        => (result.First.Select(map).ToList(), result.Second.FirstOrDefault()?.TOTAL_RECORDS ?? 0);

    private static async Task<PitDocument> LockAndMap(DapperSession s, string procedure, string company, string kind, long id)
    {
        var target = PitKinds.Target(kind); PitDocument row = kind switch
        {
            PitKinds.Declaration => PitDocumentMapper.MapDeclaration(await s.QuerySingleAsync<PitDeclaration>($"CALL {procedure}(@company,@target,@id)", new { company, target, id })),
            PitKinds.Certificate => PitDocumentMapper.MapCertificate(await s.QuerySingleAsync<PitCertificate>($"CALL {procedure}(@company,@target,@id)", new { company, target, id })),
            PitKinds.ErrorNotice => PitDocumentMapper.MapErrorNotice(await s.QuerySingleAsync<PitErrorNotice>($"CALL {procedure}(@company,@target,@id)", new { company, target, id })),
            _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
        };
        if (kind == PitKinds.Declaration) { var rows = await s.QueryAsync<PitDeclarationCertificate>("CALL getPitTkhaiCts(@company,@id)", new { company, id }); ((PitDeclarationDto)row).Certificates = rows.Select(x => new PitCertificateRegistration { TTCHUC = x.TTCHUC, SERI = x.SERI, TNGAY = XmlDate(x.TNGAY), DNGAY = XmlDate(x.DNGAY), HTHUC = x.HTHUC }).ToList(); }
        if (kind == PitKinds.ErrorNotice) { var rows = await s.QueryAsync<PitErrorNoticeCertificate>("CALL getPitTbaoCtu(@company,@id)", new { company, id }); ((PitErrorNoticeDto)row).Items = rows.Select(x => new PitNoticeItem { REF_CTU_ID = x.REF_CTU_ID, KHMSCTU = x.KHMSCTU, KHCTU = x.KHCTU, SCTU = x.SCTU, NLAP = x.NLAP, LCTDT = x.LCTDT, LDO = x.LDO ?? "" }).ToList(); }
        return row;
    }
    private static DateTimeOffset XmlDate(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
    private static DateTime SigningDate() => DateTime.UtcNow.AddHours(7).Date;

    private static long ReadCertificateNumber(string xml)
    {
        try
        {
            var value = XDocument.Parse(xml)
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "SCTu")
                ?.Value;
            return long.TryParse(value, out var number) && number > 0
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
            var value = XDocument.Parse(xml)
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "NLap")
                ?.Value;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : throw new InvalidOperationException();
        }
        catch
        {
            throw new InvalidOperationException("XML ký không có ngày lập hợp lệ.");
        }
    }
}
