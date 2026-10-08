using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IStoreInfoRepository
    {
        Task<IEnumerable<StoreInfo>> GetStoreInfoAsync(string companyCd, int? storeId = null);

        Task<int> SetStoreInfoAsync(DapperSession session, string companyCd, string userId, StoreInfoRequest request);

        /// <summary>Excel import: multi-row INSERT (no per-row setStoreInfo).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<StoreInfoRequest> records);

        Task<int> DeleteStoreInfoAsync(DapperSession session, string companyCd, int storeId, string userId);

        Task<bool> StoreCdExistsAsync(string companyCd, string storeCd, int? storeId = null, string? databaseName = null);
    }
}
