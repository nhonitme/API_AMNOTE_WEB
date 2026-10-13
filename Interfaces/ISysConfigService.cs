using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysConfigService
    {
        Task<bool> GetBoolAsync(string companyCd, string group, string key, bool defaultValue = false);
        Task<string?> GetStringAsync(string companyCd, string group, string key, string? defaultValue = null);
        Task<IReadOnlyList<SysConfig>> GetSysConfigsAsync(string companyCd, string? configGroup = null, string? configKey = null, bool activeOnly = true);
        Task<SysConfig?> GetSysConfigAsync(string companyCd, string configGroup, string configKey, bool activeOnly = true);
        Task<int> ClearCacheAsync(string? companyCd = null, string? group = null, string? key = null);
    }
}
