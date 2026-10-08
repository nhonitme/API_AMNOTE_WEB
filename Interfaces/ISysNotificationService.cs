using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysNotificationService
    {
        Task<SysNotificationListResult> GetListAsync(string companyCd, string? userId, string? roleCd, SysNotificationQueryDto query);

        Task<SysNotificationSummary> GetSummaryAsync(string companyCd, string? userId, string? roleCd);

        Task<SysNotification> CreateAsync(string companyCd, string createdBy, SysNotificationCreateRequest request);

        Task<int> MarkReadAsync(string companyCd, string? userId, string? roleCd, long? id, bool markAll);

        Task<SysNotificationSyncResultDto> SyncFromRulesAsync(string companyCd, string? userId, SysNotificationSyncRequest? request);

        Task NotifyJobResultAsync(
            string companyCd,
            string userId,
            string notificationType,
            string sourceModule,
            string sourceId,
            string title,
            string message,
            string priority,
            string? actionUrl);
    }
}
