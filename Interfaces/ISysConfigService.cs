namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysConfigService
    {
        Task<bool> GetBoolAsync(string companyCd, string group, string key, bool defaultValue = false);
        Task<string?> GetStringAsync(string companyCd, string group, string key, string? defaultValue = null);
        Task<int> ClearCacheAsync(string? companyCd = null, string? group = null, string? key = null);
    }
}
