using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysDecimalSettingRepository
    {
        Task<IEnumerable<SysDecimalSetting>> GetSettingsAsync(string companyCd, string? settingType = null);
        Task<IEnumerable<SysDecimalFieldSetting>> GetFieldSettingsAsync(string companyCd, string? fieldName = null, string? settingType = null);
        Task<SysDecimalSetting?> GetSettingAsync(string companyCd, string settingType);
        Task<SysDecimalSetting?> UpsertSettingAsync(string companyCd, string settingType, SysDecimalSettingUpdateRequest request, string userId);
    }
}
