using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Repositories
{
    public class InventoryValuationRepository : IInventoryValuationRepository
    {
        private readonly DapperExecutor _db;

        public InventoryValuationRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<InventoryValuationMovement>> GetMovementsAsync(
            string companyCd,
            string fromYmd,
            string toYmd,
            string? databaseName = null,
            string? productCds = null,
            string? storeCds = null)
        {
            const string query = @"CALL get_inventory_valuation_movements(
                @p_COMPANY_CD,
                @p_FROM_YMD,
                @p_TO_YMD,
                @p_PRODUCT_CDS,
                @p_STORE_CDS
            )";

            var items = await _db.QueryAsync<InventoryValuationMovement>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_PRODUCT_CDS = productCds,
                p_STORE_CDS = storeCds
            }, databaseName);

            return items.ToList();
        }
    }
}
