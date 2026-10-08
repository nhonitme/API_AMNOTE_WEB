using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IManagementInfoService
    {
        Task<IEnumerable<ManagementInfo>> GetListAsync(string companyCd, long? managementId = null, string? mgCd = null);
        Task<ManagementInfo?> GetByIdAsync(string companyCd, long managementId);
        Task<ManagementInfo> CreateAsync(string companyCd, string userId, ManagementInfoRequest request);
        Task<ManagementInfo> UpdateAsync(string companyCd, string userId, long managementId, ManagementInfoRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<long> managementIds);
        Task<bool> CodeExistsAsync(string companyCd, string mgCd, long? excludeId = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<ManagementInfoRequest> records, string? databaseName = null, string? lang = null);
    }
}
