using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories;

/// <summary>
/// Debt offset stored procedures own transactions. Do not wrap individual CALLs in a DapperSession.
/// New relations are keyed by COMPANY_CD, CHIT_ID and CUSTOMER_ID, never legacy *_CD or CHITDETAIL_ID.
/// </summary>
public sealed class DebtOffsetRepository
{
    private readonly DapperExecutor _db;
    public DebtOffsetRepository(DapperExecutor db) => _db = db;

    public async Task<DebtOffsetAvailable> AvailableAsync(
        string companyCd, string kind, string fromYmd, string toYmd,
        string customerIds, string currencyFilter, string? databaseName)
    {
        string query = kind == "CLEARING"
            ? "CALL sp_chit_clearing_get(@p_COMPANY_CD,@p_DEBT_TYPE,@p_FROM_YMD,@p_TO_YMD,@p_CUSTOMER_IDS,@p_FC_TYPE,@p_ACC_CD_1,@p_ACC_CD_2)"
            : "CALL sp_chit_debt_get(@p_COMPANY_CD,@p_DEBT_TYPE,@p_FROM_YMD,@p_TO_YMD,@p_CUSTOMER_IDS,@p_FC_TYPE,@p_ACC_CD)";
        var (sources, debts) = await _db.QueryMultipleAsync<DebtOffsetVoucher, DebtOffsetVoucher>(
            Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_DEBT_TYPE = kind == "COLLECT" ? "2" : "1",
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_CUSTOMER_IDS = customerIds,
                p_FC_TYPE = currencyFilter,
                p_ACC_CD = kind == "COLLECT" ? "131" : "331",
                p_ACC_CD_1 = "131",
                p_ACC_CD_2 = "331"
            }, databaseName);
        return new DebtOffsetAvailable { SOURCES = sources.ToList(), DEBTS = debts.ToList() };
    }

    public async Task<IReadOnlyList<DebtOffsetHistory>> HistoryAsync(
        string companyCd, string kind, string fromYmd, string toYmd,
        string customerIds, string currencyFilter, string? databaseName)
    {
        string query = kind == "CLEARING"
            ? "CALL sp_chit_clearing_debt_get(@p_COMPANY_CD,@p_DEBT_TYPE,@p_FROM_YMD,@p_TO_YMD,@p_CUSTOMER_IDS,@p_FC_TYPE)"
            : "CALL sp_chit_debt_cleaning_get(@p_COMPANY_CD,@p_DEBT_TYPE,@p_FROM_YMD,@p_TO_YMD,@p_CUSTOMER_IDS,@p_FC_TYPE,@p_ACC_CD)";
        var rows = await _db.QueryAsync<DebtOffsetHistory>(Net_DB.Net_DB_Company, query, new
        {
            p_COMPANY_CD = companyCd,
            p_DEBT_TYPE = kind == "COLLECT" ? "2" : "1",
            p_FROM_YMD = fromYmd,
            p_TO_YMD = toYmd,
            p_CUSTOMER_IDS = customerIds,
            p_FC_TYPE = currencyFilter,
            p_ACC_CD = kind == "COLLECT" ? "131" : "331"
        }, databaseName);
        return rows.ToList();
    }

    public async Task<DebtOffsetSaveResult> SaveAsync(
        string companyCd, string userId, DebtOffsetSaveRequest request, string? databaseName)
    {
        var kind = request.Kind!.ToUpperInvariant();
        var query = kind switch
        {
            "PAYMENT" => "CALL sp_chit_payment_upsert(@p_COMPANY_CD,0,@p_SOURCE,@p_DEBT,@p_AMOUNT,@p_AMOUNT_FC,'0',@p_CUSTOMER_ID,@p_FC_TYPE,@p_USERID)",
            "COLLECT" => "CALL sp_chit_collect_upsert(@p_COMPANY_CD,0,@p_SOURCE,@p_DEBT,@p_AMOUNT,@p_AMOUNT_FC,'0',@p_CUSTOMER_ID,@p_FC_TYPE,@p_USERID)",
            _ => "CALL sp_chit_clearing_upsert(@p_COMPANY_CD,0,0,@p_SOURCE,@p_DEBT,@p_AMOUNT,@p_AMOUNT_FC,@p_CUSTOMER_ID,@p_FC_TYPE,@p_USERID)"
        };
        var rows = await _db.QueryAsync<DebtOffsetSaveResult>(Net_DB.Net_DB_Company, query, new
        {
            p_COMPANY_CD = companyCd,
            p_SOURCE = request.SourceChitId,
            p_DEBT = request.DebtChitId,
            p_AMOUNT = request.Amount,
            p_AMOUNT_FC = request.AmountFc,
            p_CUSTOMER_ID = request.CustomerId,
            p_FC_TYPE = request.Currency,
            p_USERID = userId
        }, databaseName);
        return rows.FirstOrDefault() ?? new DebtOffsetSaveResult();
    }

    public async Task<int> DeleteAsync(
        string companyCd, string userId, string kind, long recordId, string? databaseName)
    {
        // CLEARING deletion by payment PK also soft-deletes its mirrored collect row atomically.
        string query = kind == "COLLECT"
            ? "CALL sp_chit_collect_delete(@p_COMPANY_CD,@p_ID,@p_USERID)"
            : "CALL sp_chit_payment_delete(@p_COMPANY_CD,@p_ID,@p_USERID)";
        var rows = await _db.QueryAsync<DebtOffsetDeleteResult>(Net_DB.Net_DB_Company, query,
            new { p_COMPANY_CD = companyCd, p_ID = recordId, p_USERID = userId }, databaseName);
        return rows.FirstOrDefault()?.DELETED_COUNT ?? 0;
    }
}
