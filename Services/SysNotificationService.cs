using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services
{
    public class SysNotificationService : ISysNotificationService
    {
        private readonly ISysNotificationRepository _repository;
        private readonly ILogger<SysNotificationService> _logger;

        public SysNotificationService(ISysNotificationRepository repository, ILogger<SysNotificationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public Task<SysNotificationListResult> GetListAsync(
            string companyCd,
            string? userId,
            string? roleCd,
            SysNotificationQueryDto query)
            => _repository.GetListAsync(companyCd, userId, roleCd, query);

        public async Task<SysNotificationSummary> GetSummaryAsync(string companyCd, string? userId, string? roleCd)
        {
            var summary = await _repository.GetSummaryAsync(companyCd, userId, roleCd);
            return summary ?? new SysNotificationSummary();
        }

        public async Task<SysNotification> CreateAsync(string companyCd, string createdBy, SysNotificationCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NotificationType))
            {
                throw new ArgumentException("NotificationType is required");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException("Title is required");
            }

            var id = await _repository.SaveAsync(companyCd, createdBy, request);
            if (id <= 0)
            {
                throw new InvalidOperationException("Create notification failed");
            }

            return new SysNotification
            {
                ID = id,
                COMPANY_CD = companyCd,
                USER_ID = request.UserId,
                ROLE_CD = request.RoleCd,
                NOTIFICATION_TYPE = request.NotificationType.Trim(),
                SOURCE_MODULE = request.SourceModule,
                SOURCE_ID = request.SourceId,
                TITLE = request.Title.Trim(),
                MESSAGE = request.Message,
                PRIORITY = string.IsNullOrWhiteSpace(request.Priority) ? "MEDIUM" : request.Priority.Trim().ToUpperInvariant(),
                ACTION_URL = request.ActionUrl,
                IS_READ = "N",
                CREATE_BY = createdBy,
                CREATE_AT = DateTime.Now
            };
        }

        public Task<int> MarkReadAsync(string companyCd, string? userId, string? roleCd, long? id, bool markAll)
            => _repository.MarkReadAsync(companyCd, userId, roleCd, id, markAll);

        public async Task<SysNotificationSyncResultDto> SyncFromRulesAsync(
            string companyCd,
            string? userId,
            SysNotificationSyncRequest? request)
        {
            var today = DateTime.Today;
            var fromYmd = Common.NormalizeNullableText(request?.FromYmd)
                ?? new DateTime(today.Year, today.Month, 1).ToString("yyyyMMdd");
            var toYmd = Common.NormalizeNullableText(request?.ToYmd)
                ?? today.ToString("yyyyMMdd");

            Common.ValidateYmdRange(fromYmd, toYmd);

            var syncedCount = await _repository.SyncFromRulesAsync(companyCd, userId, fromYmd, toYmd);
            return new SysNotificationSyncResultDto { SyncedCount = syncedCount };
        }

        public async Task NotifyJobResultAsync(
            string companyCd,
            string userId,
            string notificationType,
            string sourceModule,
            string sourceId,
            string title,
            string message,
            string priority,
            string? actionUrl)
        {
            await _repository.SaveAsync(
                companyCd,
                "SYSTEM_JOB",
                new SysNotificationCreateRequest
                {
                    UserId = userId,
                    NotificationType = notificationType,
                    SourceModule = sourceModule,
                    SourceId = sourceId,
                    Title = title,
                    Message = message,
                    Priority = priority,
                    ActionUrl = actionUrl
                });

            _logger.LogInformation(
                "Job notification created. CompanyCd={CompanyCd}, UserId={UserId}, Type={NotificationType}, SourceId={SourceId}",
                companyCd,
                userId,
                notificationType,
                sourceId);
        }
    }
}
