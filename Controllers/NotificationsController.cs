using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public class NotificationsController : BaseApiController
    {
        private readonly ISysNotificationService _service;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(ISysNotificationService service, ILogger<NotificationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] string? isRead = null,
            [FromQuery] string? notificationType = null,
            [FromQuery] string? sourceModule = null,
            [FromQuery] string? keyword = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var query = new SysNotificationQueryDto
            {
                IsRead = isRead,
                NotificationType = notificationType,
                SourceModule = sourceModule,
                Keyword = keyword,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _service.GetListAsync(companyCd, userId, null, query);
            return PagedSuccess(result.Items, query.PageNumber, query.PageSize, result.TotalCount);
        }

        [HttpGet("summary")]
        [Authorize]
        public async Task<IActionResult> GetSummary()
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var summary = await _service.GetSummaryAsync(companyCd, userId, null);
            return Success(summary);
        }

        [HttpPost("sync")]
        [Authorize]
        public async Task<IActionResult> SyncFromRules([FromBody] SysNotificationSyncRequest? request)
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var result = await _service.SyncFromRulesAsync(companyCd, userId, request);
            _logger.LogInformation("Notification sync requested by {UserId}. SyncedCount={SyncedCount}", userId, result.SyncedCount);
            return Success(result, "Notifications synchronized");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateNotification([FromBody] SysNotificationCreateRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var entity = await _service.CreateAsync(companyCd, userId, request);
            return Created(entity, "Notification created");
        }

        [HttpPut("{id:long}/read")]
        [Authorize]
        public async Task<IActionResult> MarkRead([FromRoute] long id)
        {
            if (id <= 0)
            {
                return ValidationError("Notification id is invalid");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var affectedRows = await _service.MarkReadAsync(companyCd, userId, null, id, markAll: false);
            if (affectedRows <= 0)
            {
                return NotFound("Notification not found");
            }

            return Updated(new { Id = id }, "Notification marked as read");
        }

        [HttpPut("read-all")]
        [Authorize]
        public async Task<IActionResult> MarkAllRead()
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var affectedRows = await _service.MarkReadAsync(companyCd, userId, null, null, markAll: true);
            return Updated(new { AffectedRows = affectedRows }, "All notifications marked as read");
        }
    }
}
