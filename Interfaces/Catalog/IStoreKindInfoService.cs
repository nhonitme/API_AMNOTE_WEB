using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IStoreKindInfoService
    {
        Task<IEnumerable<StoreKindInfo>> GetListAsync(string companyCd, int? storeKindId = null, string? storeKindCd = null);
        Task<StoreKindInfo?> GetByIdAsync(string companyCd, int storeKindId);
        Task<StoreKindInfo> CreateAsync(string companyCd, string userId, StoreKindInfoRequest request);
        Task<StoreKindInfo> UpdateAsync(string companyCd, string userId, int storeKindId, StoreKindInfoRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<int> storeKindIds);
        Task<bool> CodeExistsAsync(string companyCd, string storeKindCd, int? excludeId = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<StoreKindInfoRequest> records, string? databaseName = null, string? lang = null);
    }
}
