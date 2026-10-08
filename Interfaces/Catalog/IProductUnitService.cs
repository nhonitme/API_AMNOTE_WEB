using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IProductUnitService
    {
        Task<IEnumerable<ProductUnit>> GetListAsync(string companyCd, int? unitId = null, string? unitCd = null);
        Task<ProductUnit?> GetByIdAsync(string companyCd, int unitId);
        Task<ProductUnit> CreateAsync(string companyCd, string userId, ProductUnit request);
        Task<ProductUnit> UpdateAsync(string companyCd, string userId, int unitId, ProductUnit request);
        Task<int> DeleteAsync(string companyCd, string userId, List<int> unitIds);
        Task<bool> CodeExistsAsync(string companyCd, string unitCd, int? excludeId = null, string? databaseName = null, string? lang = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<ProductUnit> records, string? databaseName = null, string? lang = null);
    }
}
