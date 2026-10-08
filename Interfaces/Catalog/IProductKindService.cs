using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IProductKindService
    {
        Task<IEnumerable<ProductKind>> GetListAsync(string companyCd, int? productKindId = null, string? productKindCd = null);
        Task<ProductKind?> GetByIdAsync(string companyCd, int productKindId);
        Task<ProductKind> CreateAsync(string companyCd, string userId, ProductKind request);
        Task<ProductKind> UpdateAsync(string companyCd, string userId, int productKindId, ProductKind request);
        Task<int> DeleteAsync(string companyCd, string userId, List<int> productKindIds);
        Task<bool> CodeExistsAsync(string companyCd, string productKindCd, int? excludeId = null, string? databaseName = null, string? lang = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<ProductKind> records, string? databaseName = null, string? lang = null);
    }
}
