using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysDecimalSettingService
    {
        Task<IEnumerable<SysDecimalSettingDto>> GetSettingsAsync(string companyCd, string? settingType = null);
        Task<IEnumerable<SysDecimalFieldSettingDto>> GetFieldSettingsAsync(string companyCd, string? fieldName = null, string? settingType = null);
        Task<SysDecimalSetting?> GetSettingAsync(string companyCd, string settingType);
        Task<SysDecimalSettingDto?> UpsertSettingAsync(string companyCd, string settingType, SysDecimalSettingUpdateRequest request, string userId);
    }
}
