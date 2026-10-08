using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysNotificationRepository
    {
        Task<SysNotificationListResult> GetListAsync(
            string companyCd,
            string? userId,
            string? roleCd,
            SysNotificationQueryDto query);

        Task<SysNotificationSummary?> GetSummaryAsync(string companyCd, string? userId, string? roleCd);

        Task<long> SaveAsync(string companyCd, string createdBy, SysNotificationCreateRequest request, long? id = null);

        Task<int> MarkReadAsync(string companyCd, string? userId, string? roleCd, long? id, bool markAll);

        Task<int> SyncFromRulesAsync(string companyCd, string? userId, string fromYmd, string toYmd);
    }
}
