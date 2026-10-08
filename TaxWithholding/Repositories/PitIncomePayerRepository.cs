using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitIncomePayerRepository(DapperExecutor db)
{
    public async Task<PitIncomePayer?> GetAsync(string companyCd)
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
