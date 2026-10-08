using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IDepartmentInfoService
    {
        Task<IEnumerable<DepartmentInfo>> GetListAsync(string companyCd, long? departmentId = null, string? departmentCd = null);
        Task<DepartmentInfo?> GetByIdAsync(string companyCd, long departmentId);
        Task<DepartmentInfo> CreateAsync(string companyCd, string userId, DepartmentInfoRequest request);
        Task<DepartmentInfo> UpdateAsync(string companyCd, string userId, long departmentId, DepartmentInfoRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<long> departmentIds);
        Task<bool> CodeExistsAsync(string companyCd, string departmentCd, long? excludeId = null, string? databaseName = null, string? lang = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<DepartmentInfoRequest> records, string? databaseName = null, string? lang = null);
    }
}
