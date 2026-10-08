using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IAcclistInfoService
    {
        Task<IEnumerable<AcclistInfo>> GetListAsync(string companyCd, int? accId = null);
        Task<AcclistInfo?> GetByIdAsync(string companyCd, int accId);
        Task<AcclistInfo> CreateAsync(string companyCd, string userId, AcclistInfoRequest request);
        Task<AcclistInfo> UpdateAsync(string companyCd, string userId, int accId, AcclistInfoRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<int> accIds);
        Task<bool> CodeExistsAsync(string companyCd, string accCd, int? excludeId = null);
    }
}
