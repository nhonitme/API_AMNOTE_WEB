using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IMasterInUseRepository
    {
        Task<MasterInUseResult> CheckInUseAsync(string companyCd, string masterType, long masterId, string? masterCd = null, string? databaseName = null);
    }
}
