using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysGridColumnSettingRepository
    {
        Task<SysGridColumnBundleDto> GetAllAsync(string companyCd, string userId);
        Task<SysGridColumnBundleDto> SaveAsync(
            string companyCd,
            string userId,
            string gridId,
            long templateId,
            string? templateName,
            string? isDefaultTemplate,
            IEnumerable<SysGridColumnSettingSaveItemRequest> columns,
            string actor);
        Task<SysGridColumnBundleDto> ResetAsync(string companyCd, string userId, string gridId, string actor);
        Task<IReadOnlyList<SysGridColumn>> GetMergedLayoutAsync(string companyCd, string userId, string gridId, long? templateId = null);
    }
}
