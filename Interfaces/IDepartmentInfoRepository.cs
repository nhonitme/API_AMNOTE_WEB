using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IDepartmentInfoRepository
    {
        Task<IEnumerable<DepartmentInfo>> GetDepartmentInfoAsync(string companyCd, long? departmentId = null, string? departmentCd = null);

        Task<int> SetDepartmentInfoAsync(DapperSession session, string companyCd, string userId, DepartmentInfoRequest request);

        /// <summary>Excel import: multi-row INSERT (no per-row setDepartmentInfo).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<DepartmentInfoRequest> records);

        Task<int> DeleteDepartmentInfoAsync(DapperSession session, string companyCd, long departmentId, string userId);

        Task<IEnumerable<DepartmentLookupItem>> GetDepartmentLookupAsync(string companyCd, string lang = "VIET");

        Task<bool> DepartmentCdExistsAsync(string companyCd, string departmentCd, long? departmentId = null, string? databaseName = null);
    }
}
