using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Interfaces;
using System.Globalization;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitCertificateService(
    DapperExecutor db,
    IPitCertificateRepository repository,
    IPitRepository documents,
    IPitXslService templates,
    ISystemRepository systemRepository) : IPitCertificateService
{
    public async Task<long> SaveAsync(string company, string user, long id, int version, long? xslId, PitCertificateDto data)
    {
        PitCertificateValidator.Validate(data);
        await RequireActiveIncomeTypeAsync(data.KTNHAP);
        if (xslId is not > 0) throw new ArgumentException("Chọn mẫu số / ký hiệu chứng từ từ danh mục.");
        var catalog = await templates.RequireActiveCatalogAsync(company, xslId.Value);
        if (!string.Equals(data.KHCTU, catalog.SERIES, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ký hiệu không khớp mẫu số đã chọn trong danh mục.");
        data.KHCTU = catalog.SERIES!;
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

    private async Task RequireActiveIncomeTypeAsync(int value)
    {
        if (value is < 1 or > 8) throw new ArgumentException("Khoản thu nhập phải là mã từ 1 đến 8 theo danh mục.");
        var code = value.ToString(CultureInfo.InvariantCulture);
        var codes = await systemRepository.GetSysCodeAsync(PitDocumentRules.IncomeTypeCodeType);
        if (!codes.Any(item => item.CODE_CD == code && (item.IS_ACTIVE is null or 1) && item.ISDEL != "1"))
            throw new ArgumentException("Khoản thu nhập chưa được cấu hình hoặc đã ngừng sử dụng. Vui lòng tải lại danh mục và chọn lại.");
    }
}
