using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IStoreInfoService
    {
        Task<IEnumerable<StoreInfo>> GetListAsync(string companyCd, int? storeId = null, string? storeCd = null);
        Task<StoreInfo?> GetByIdAsync(string companyCd, int storeId);
        Task<StoreInfo> CreateAsync(string companyCd, string userId, StoreInfoRequest request);
        Task<StoreInfo> UpdateAsync(string companyCd, string userId, int storeId, StoreInfoRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<int> storeIds);
        Task<bool> CodeExistsAsync(string companyCd, string storeCd, int? excludeId = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<StoreInfoRequest> records, string? databaseName = null, string? lang = null);
    }
}
