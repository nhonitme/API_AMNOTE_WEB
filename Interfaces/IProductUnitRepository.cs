using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IProductUnitRepository
    {
        Task<IEnumerable<ProductUnit>> GetProductUnitAsync(string companyCd, int? unitId = null, string? unitCd = null);

        Task<int> SetProductUnitAsync(DapperSession session, string companyCd, string userId, ProductUnit productUnit);

        /// <summary>Excel import: multi-row INSERT (no per-row setProductUnit).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<ProductUnit> records);

        Task<int> DeleteProductUnitAsync(DapperSession session, string companyCd, int unitId, string userId);

        Task<bool> ProductUnitCdExistsAsync(string companyCd, string unitCd, int? unitId = null, string? databaseName = null);
    }
}
