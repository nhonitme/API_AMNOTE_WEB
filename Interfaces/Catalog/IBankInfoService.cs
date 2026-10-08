using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IBankInfoService
    {
        Task<IEnumerable<BankInfo>> GetListAsync(string companyCd, long? bankId = null, string? bankCd = null);
        Task<BankInfo?> GetByIdAsync(string companyCd, long bankId);
        Task<BankInfo> CreateAsync(string companyCd, string userId, BankInfoRequest request);
        Task<BankInfo> UpdateAsync(string companyCd, string userId, long bankId, BankInfoRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<long> bankIds);
        Task<bool> CodeExistsAsync(string companyCd, string bankCd, long? excludeId = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<BankInfoRequest> records, string? databaseName = null, string? lang = null);
    }
}
