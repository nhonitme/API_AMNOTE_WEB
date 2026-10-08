using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysConfigRepository
    {
        Task<IEnumerable<SysConfig>> GetSysConfigsAsync(string companyCd, string? configGroup = null, string? configKey = null, bool activeOnly = true);
        Task<SysConfig?> GetSysConfigAsync(string companyCd, string configGroup, string configKey, bool activeOnly = true);
    }
}
