using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IAcclistInfoRepository
    {
        Task<IEnumerable<AcclistInfo>> GetAcclistInfoAsync(string companyCd, int? accId = null);

        Task<int> SetAcclistInfoAsync(DapperSession session, string companyCd, string userId, AcclistInfoRequest request);

        Task<int> DeleteAcclistInfoAsync(DapperSession session, string companyCd, int accId, string userId);

        Task<bool> AccCdExistsAsync(string companyCd, string accCd, int? accId = null, string? databaseName = null);
    }
}
