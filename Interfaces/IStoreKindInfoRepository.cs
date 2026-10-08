using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IStoreKindInfoRepository
    {
        Task<IEnumerable<StoreKindInfo>> GetStoreKindInfoAsync(string companyCd, int? storeKindId = null, string? databaseName = null);

        Task<int> SetStoreKindInfoAsync(DapperSession session, string companyCd, string userId, StoreKindInfoRequest request);

        /// <summary>Excel import: multi-row INSERT (no per-row setStoreKindInfo).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<StoreKindInfoRequest> records);

        Task<int> DeleteStoreKindInfoAsync(DapperSession session, string companyCd, int storeKindId, string userId);

        Task<IEnumerable<LookupItem>> GetStoreKindLookupAsync(string companyCd, int? storeKindId = null, string lang = "VIET");

        Task<bool> StoreKindCdExistsAsync(string companyCd, string storeKindCd, int? storeKindId = null, string? databaseName = null);
    }
}
