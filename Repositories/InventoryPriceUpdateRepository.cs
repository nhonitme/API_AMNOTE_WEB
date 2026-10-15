using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories;

public sealed class InventoryPriceUpdateRepository
{
    private readonly DapperExecutor _db;

    public InventoryPriceUpdateRepository(DapperExecutor db) => _db = db;

    public async Task<IReadOnlyList<InventoryPriceUpdateRow>> GetRowsAsync(
        string flow, string companyCd, string databaseName, string storeCd, string fromYmd, string toYmd)
    {
        var procedure = flow == "IN" ? "get_inventory_update_in_price" : "get_inventory_update_out_price";
        var rows = await _db.QueryAsync<InventoryPriceUpdateRow>(Net_DB.Net_DB_Company,
            $"CALL {procedure}(@p_COMPANY_CD,@p_STORE_CD,@p_FROM_YMD,@p_TO_YMD)",
            new { p_COMPANY_CD = companyCd, p_STORE_CD = storeCd, p_FROM_YMD = fromYmd, p_TO_YMD = toYmd },
            databaseName);
        return rows.ToList();
    }

    public async Task ApplyAsync(
        string flow, string companyCd, string databaseName, string userId,
        string storeCd, string fromYmd, string toYmd, IReadOnlyList<InventoryPriceUpdateRow> rows)
    {
        var procedure = flow == "IN" ? "apply_inventory_update_in_price" : "apply_inventory_update_out_price";
        await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);
        try
        {
            foreach (var row in rows)
            {
                await session.ExecuteAsync($"CALL {procedure}(@p_COMPANY_CD,@p_STORE_CD,@p_FROM_YMD,@p_TO_YMD,@p_PRODUCT_CD,@p_NEW_PRICE,@p_USER)", new
                {
                    p_COMPANY_CD = companyCd,
                    p_STORE_CD = storeCd,
                    p_FROM_YMD = fromYmd,
                    p_TO_YMD = toYmd,
                    p_PRODUCT_CD = row.PRODUCT_CD,
                    p_NEW_PRICE = row.NEW_UNIT_PRICE,
                    p_USER = userId
                }, commandTimeout: 120);
            }
            session.Commit();
        }
        catch
        {
            session.Rollback();
            throw;
        }
    }
}
