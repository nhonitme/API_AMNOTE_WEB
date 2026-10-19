using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class ProductMaterialInfoRepository
    {
        private readonly DapperExecutor _db;
        public ProductMaterialInfoRepository(DapperExecutor db) => _db = db;

        public async Task<IReadOnlyList<ProductMaterialInfoDto>> GetListAsync(string companyCd, long productId)
        {
            var rows = await _db.QueryAsync<ProductMaterialInfoDto>(
                Net_DB.Net_DB_Company,
                "CALL sp_product_material_info_get(@p_COMPANY_CD, @p_PRODUCT_ID)",
                new { p_COMPANY_CD = companyCd, p_PRODUCT_ID = productId });
            return rows.ToList();
        }

        public async Task<IReadOnlyList<ProductMaterialCountDto>> GetSummaryAsync(string companyCd)
        {
            var rows = await _db.QueryAsync<ProductMaterialCountDto>(
                Net_DB.Net_DB_Company,
                "CALL sp_product_material_summary_get(@p_COMPANY_CD)",
                new { p_COMPANY_CD = companyCd });
            return rows.ToList();
        }

        public async Task<long> SaveAsync(string companyCd, string userId, long materialId, ProductMaterialSaveRequest request)
        {
            var ids = await _db.QueryAsync<long>(
                Net_DB.Net_DB_Company,
                @"CALL sp_product_material_info_upsert(
                    @p_MATERIAL_ID, @p_COMPANY_CD, @p_PRODUCT_ID,
                    @p_MATERIAL_PRODUCT_ID, @p_QUANTITY, @p_CREATE_BY, @p_UPDATE_BY)",
                new {
                    p_MATERIAL_ID = materialId,
                    p_COMPANY_CD = companyCd,
                    p_PRODUCT_ID = request.PRODUCT_ID,
                    p_MATERIAL_PRODUCT_ID = request.MATERIAL_PRODUCT_ID,
                    p_QUANTITY = request.QUANTITY,
                    p_CREATE_BY = userId,
                    p_UPDATE_BY = userId
                });
            return ids.First();
        }

        public Task<int> DeleteAsync(string companyCd, string userId, long materialId)
            => _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                "CALL sp_product_material_info_delete(@p_COMPANY_CD, @p_MATERIAL_ID, @p_UPDATE_BY)",
                new { p_COMPANY_CD = companyCd, p_MATERIAL_ID = materialId, p_UPDATE_BY = userId });
    }
}
