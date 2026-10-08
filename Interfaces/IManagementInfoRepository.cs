using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IManagementInfoRepository
    {
        Task<IEnumerable<ManagementInfo>> GetManagementInfoAsync(string companyCd, long? managementId = null, string? mgCd = null);

        Task<int> SetManagementInfoAsync(DapperSession session, string companyCd, string userId, ManagementInfoRequest request);

        /// <summary>Excel import: multi-row INSERT (no per-row setManagementInfo).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<ManagementInfoRequest> records);

        Task<int> DeleteManagementInfoAsync(DapperSession session, string companyCd, long managementId, string userId);

        Task<bool> MgCdExistsAsync(string companyCd, string mgCd, long? managementId = null, string? databaseName = null);
    }
}
