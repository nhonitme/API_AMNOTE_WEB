using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using System.Globalization;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitCertificateService(
    DapperExecutor db,
    IPitCertificateRepository repository,
    IPitRepository documents,
    IPitXslService templates,
    ISystemRepository systemRepository,
    ILogger<PitCertificateService> logger) : IPitCertificateService
{
    private const string PublicLookupCompanyTargetsQuery = "CALL getEInvoicePublicLookupCompanyTargets(@p_TAX_CODE)";

    private sealed class PublicLookupCompanyTarget
    {
        public string COMPANY_CD { get; set; } = "";
        public string DB_NAME { get; set; } = "";
    }

    public async Task<long> SaveAsync(string company, string user, long id, int version, long? xslId, PitCertificateDto data)
    {
        PitCertificateValidator.Validate(data);
        await RequireActiveIncomeTypeAsync(data.KTNHAP);
        if (xslId is not > 0) throw new ArgumentException("Chọn mẫu số / ký hiệu chứng từ từ danh mục.");
        var catalog = await templates.RequireActiveCatalogAsync(company, xslId.Value);
        if (!string.Equals(data.KHCTU, catalog.SERIES, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ký hiệu không khớp mẫu số đã chọn trong danh mục.");
        data.KHCTU = catalog.SERIES!;
        data.MTRACUU = NormalizeLookupCode(data.MTRACUU);
        await using var session = await db.CreateSessionAsync();
        var savedId = await repository.SaveAsync(session, company, user, id, version, xslId, data);
        session.Commit();
        return savedId;
    }

    public async Task ValidateSigningAsync(string company, long id, bool acceptingSignature = false)
    {
        var document = await documents.Get(company, PitKinds.Certificate, id);
        if (document.XSL_ID is not > 0) throw new ArgumentException("Chọn mẫu số / ký hiệu chứng từ từ danh mục.");
        await templates.RequireActiveCatalogAsync(company, document.XSL_ID.Value);
        // Preserve idempotent acknowledgement of an already signed document.
        if (!acceptingSignature || document.IS_SIGNED != 1)
            await RequireActiveIncomeTypeAsync(((PitCertificateDto)document).KTNHAP);
    }

    public async Task<PitCertificatePublicLookupDto?> LookupPublicAsync(string taxCode, string lookupCode)
    {
        var found = await FindPublicAsync(taxCode, lookupCode);
        return found == null ? null : ToPublicLookupDto(found.Value.Row);
    }

    public async Task<PitCertificatePublicDownloadInfo?> GetPublicDownloadInfoAsync(string taxCode, string lookupCode)
    {
        var found = await FindPublicAsync(taxCode, lookupCode);
        if (found == null) return null;
        var row = found.Value.Row;
        return new PitCertificatePublicDownloadInfo
        {
            CTU_ID = row.CTU_ID,
            COMPANY_CD = found.Value.CompanyCd,
            DB_NAME = found.Value.DatabaseName,
            MTRACUU = row.MTRACUU,
            KHCTU = row.KHCTU,
            SCTU = row.SCTU,
        };
    }

    public async Task<string?> GetPublicXmlAsync(string taxCode, string lookupCode)
    {
        var found = await FindPublicAsync(taxCode, lookupCode);
        if (found == null) return null;
        var row = found.Value.Row;
        if (!string.IsNullOrWhiteSpace(row.SIGNED_XML)) return row.SIGNED_XML;
        if (!string.IsNullOrWhiteSpace(row.RAW_XML)) return row.RAW_XML;
        return PitCertificateXmlBuilder.Build(PitDocumentMapper.MapCertificate(row), row.CTU_ID, row.DOC_VERSION, row.SCTU);
    }

    private async Task<(PitCertificate Row, string CompanyCd, string DatabaseName)?> FindPublicAsync(string taxCode, string lookupCode)
    {
        var normalizedLookupCode = NormalizeLookupCode(lookupCode);
        if (string.IsNullOrWhiteSpace(normalizedLookupCode) || !IsSafeLookupCode(normalizedLookupCode))
            throw new ArgumentException("MTRACUU is invalid");

        var normalizedTaxCode = NormalizeTaxCode(taxCode)
            ?? throw new ArgumentException("TAX_CD is required");

        var targets = (await db.QueryAsync<PublicLookupCompanyTarget>(
                Net_DB.Net_DB_Manager,
                PublicLookupCompanyTargetsQuery,
                new { p_TAX_CODE = normalizedTaxCode }))
            .Where(x => !string.IsNullOrWhiteSpace(x.COMPANY_CD) && !string.IsNullOrWhiteSpace(x.DB_NAME))
            .ToList();

        foreach (var target in targets)
        {
            try
            {
                var row = await repository.GetByLookupAsync(target.COMPANY_CD, normalizedLookupCode, target.DB_NAME);
                if (row == null) continue;
                if (!string.Equals(NormalizeTaxCode(row.TCTTNHAP_MST), normalizedTaxCode, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(NormalizeTaxCode(row.NNT_MST), normalizedTaxCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return (row, target.COMPANY_CD, target.DB_NAME);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Public PIT certificate lookup skipped company after database query failed. Company: {CompanyCd}, DbName: {DbName}",
                    target.COMPANY_CD,
                    target.DB_NAME);
            }
        }

        return null;
    }

    private static PitCertificatePublicLookupDto ToPublicLookupDto(PitCertificate row)
        => new()
        {
            MTRACUU = row.MTRACUU,
            TCTU = string.IsNullOrWhiteSpace(row.TCTU) ? "Chứng từ khấu trừ thuế thu nhập cá nhân" : row.TCTU,
            MSCTU = string.IsNullOrWhiteSpace(row.MSCTU) ? "03/TNCN" : row.MSCTU!,
            KHCTU = row.KHCTU,
            SCTU = row.SCTU,
            NLAP = row.NLAP,
            TCTTNHAP_TEN = row.TCTTNHAP_TEN,
            TCTTNHAP_MST = row.TCTTNHAP_MST,
            TCTTNHAP_DCHI = row.TCTTNHAP_DCHI,
            NNT_TEN = row.NNT_TEN,
            NNT_MST = row.NNT_MST,
            NNT_DCHI = row.NNT_DCHI,
            KTNHAP = row.KTNHAP,
            TTHANG = row.TTHANG,
            DTHANG = row.DTHANG,
            NAM = row.NAM,
            BHIEM = row.BHIEM,
            TTHIEN = row.TTHIEN,
            TTNCTHUE = row.TTNCTHUE,
            TTNTTHUE = row.TTNTTHUE,
            STHUE = row.STHUE,
            CAN_DOWNLOAD_PDF = true,
            CAN_DOWNLOAD_XML = !string.IsNullOrWhiteSpace(row.SIGNED_XML) || !string.IsNullOrWhiteSpace(row.RAW_XML) || row.CTU_ID > 0,
        };

    private async Task RequireActiveIncomeTypeAsync(int value)
    {
        if (value is < 1 or > 8) throw new ArgumentException("Khoản thu nhập phải là mã từ 1 đến 8 theo danh mục.");
        var code = value.ToString(CultureInfo.InvariantCulture);
        var codes = await systemRepository.GetSysCodeAsync(PitDocumentRules.IncomeTypeCodeType);
        if (!codes.Any(item => item.CODE_CD == code && (item.IS_ACTIVE is null or 1) && item.ISDEL != "1"))
            throw new ArgumentException("Khoản thu nhập chưa được cấu hình hoặc đã ngừng sử dụng. Vui lòng tải lại danh mục và chọn lại.");
    }

    private static string? NormalizeLookupCode(string? value)
    {
        var text = Common.NormalizeNullableText(value);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }

    private static string? NormalizeTaxCode(string? value)
    {
        var text = Common.NormalizeNullableText(value);
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = new string(text
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsSafeLookupCode(string value)
        => value.Length is >= 8 and <= 100 && value.All(char.IsLetterOrDigit);
}
