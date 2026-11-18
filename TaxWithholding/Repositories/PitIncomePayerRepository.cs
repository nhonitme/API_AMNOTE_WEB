using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitIncomePayerRepository(DapperExecutor db, ICompanyInfoRepository companyInfo) : IPitIncomePayerRepository
{
    public async Task<PitIncomePayer?> GetAsync(string companyCd)
    {
        var existing = await ReadAsync(companyCd);
        if (existing != null) return existing;

        var company = await companyInfo.GetCompanyInfoAsync(companyCd);
        if (company == null) return null;

        // MySQL 8.4.11: a concurrent initializer/manual save wins without being overwritten.
        await db.ExecuteAsync(Net_DB.Net_DB_Company, """
            INSERT INTO pit_income_payer
                (COMPANY_CD, PAYER_NM, TAX_CD, ADDRESS, PHONE, EMAIL, CREATE_BY, CREATE_AT)
            VALUES (@company, @name, @tax, @address, @phone, @email, @user, NOW())
            ON DUPLICATE KEY UPDATE COMPANY_CD = @company
            """, new
        {
            company = companyCd,
            name = company.COMPANY_NM?.Trim() ?? "",
            tax = company.TAX_CD?.Trim() ?? "",
            address = company.ADDRESS?.Trim() ?? "",
            phone = Common.NormalizeNullableText(company.TEL) is { } phone ? phone[..Math.Min(phone.Length, 20)] : null,
            email = Common.NormalizeNullableText(company.EMAIL),
            user = Common.GetUserId()
        });

        return await ReadAsync(companyCd);
    }

    private async Task<PitIncomePayer?> ReadAsync(string companyCd)
        => (await db.QueryAsync<PitIncomePayer>(
            Net_DB.Net_DB_Company,
            "CALL getPitIncomePayer(@company)",
            new { company = companyCd })).FirstOrDefault();

    public async Task<PitIncomePayer> SaveAsync(
        string companyCd,
        string userId,
        PitIncomePayerSaveRequest request)
    {
        await using var session = await db.CreateSessionAsync(Net_DB.Net_DB_Company);
        var saved = await session.QuerySingleAsync<PitIncomePayer>(
            "CALL setPitIncomePayer(@company,@payer_nm,@tax_cd,@address,@phone,@email,@user)",
            new
            {
                company = companyCd,
                payer_nm = request.PAYER_NM,
                tax_cd = request.TAX_CD,
                address = request.ADDRESS,
                phone = request.PHONE,
                email = request.EMAIL,
                user = userId
            });
        session.Commit();
        return saved;
    }
}
