using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IBankInfoRepository
    {
        Task<IEnumerable<BankInfo>> GetBankInfoAsync(string companyCd, long? bankId = null, string? bankCd = null);

        Task<int> SetBankInfoAsync(DapperSession session, string companyCd, string userId, BankInfoRequest request);

        /// <summary>Excel import: multi-row INSERT (no per-row setBankInfo).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<BankInfoRequest> records);

        Task<int> DeleteBankInfoAsync(DapperSession session, string companyCd, long bankId);

        Task<bool> BankCdExistsAsync(string companyCd, string bankCd, long? bankId = null, string? databaseName = null);
    }
}
