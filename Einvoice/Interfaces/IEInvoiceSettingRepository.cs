using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceSettingRepository
    {
        Task<IEnumerable<EInvoiceDecimalSetting>> GetDecimalSettingsAsync(string companyCd, long? settingId, string? applyTarget, string? fieldScope, string? keyword, bool includeInactive, long? xslId = null);
        Task ClearDecimalSettingsCacheAsync(string companyCd);
        Task<long> SetDecimalSettingAsync(DapperSession session, string companyCd, string userId, EInvoiceDecimalSetting setting);
        Task<IEnumerable<EInvoiceUserSetting>> GetUserSettingsAsync(string companyCd, long? settingId, string? userId, string? keyword, bool includeDeleted);
        Task ClearUserSettingsCacheAsync(string companyCd);
        Task<long> SetUserSettingAsync(DapperSession session, string userId, EInvoiceUserSetting setting);
        Task<IEnumerable<EInvoiceAdminSetting>> GetAdminSettingsAsync(string companyCd, long? settingId, string? settingType, string? keyword, bool includeDeleted);
    }
}
