using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IUserSettingService
    {
        Task<UserSettingResolvedDto?> GetSettingAsync(string companyCd, string userId, string keyName, string? defaultValue = null);
        Task<IReadOnlyList<UserSettingResolvedDto>> GetSettingsAsync(string companyCd, string userId);
        Task<IReadOnlyList<UserSettingResolvedDto>> GetSettingsAsync(string companyCd, string userId, IEnumerable<string> keyNames);
        Task<string?> GetStringAsync(string companyCd, string userId, string keyName, string? defaultValue = null);
        Task<bool> GetBoolAsync(string companyCd, string userId, string keyName, bool defaultValue = false);
        Task<int?> GetIntAsync(string companyCd, string userId, string keyName, int? defaultValue = null);
        Task<UserSettingResolvedDto> SaveAsync(string companyCd, string userId, UserSettingSaveRequest request);
        Task<IReadOnlyList<UserSettingResolvedDto>> SaveBulkAsync(string companyCd, string userId, IEnumerable<UserSettingSaveRequest> items);
        Task<bool> DeleteAsync(string companyCd, string userId, string keyName);
        Task<int> ClearCacheAsync(string? companyCd = null, string? userId = null, string? keyName = null);
    }
}
