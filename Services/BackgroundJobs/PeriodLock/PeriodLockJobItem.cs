using API_AMNOTE_WEB.Models;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock
{
    /// <summary>
    /// Item đưa vào queue background xử lý khóa/mở sổ.
    /// </summary>
    public sealed class PeriodLockJobItem
    {
        /// <summary>Loại job: LOCK hoặc UNLOCK.</summary>
        public string JobType { get; set; } = PeriodLockJobType.Lock;

        /// <summary>Mã công ty/tenant.</summary>
        public string CompanyCd { get; set; } = string.Empty;

        /// <summary>Tên DB công ty đã resolve lúc enqueue (giống Excel import).</summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>Mã job dùng để frontend polling progress.</summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>User thực hiện thao tác.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Request khóa sổ. Có giá trị khi JobType = LOCK.</summary>
        public StartPeriodLockRequest? LockRequest { get; set; }

        /// <summary>Request mở sổ. Có giá trị khi JobType = UNLOCK.</summary>
        public StartPeriodUnlockRequest? UnlockRequest { get; set; }

        /// <summary>
        /// Claims của user tại thời điểm gọi API.
        /// BackgroundService dùng để tạo lại HttpContext.User.
        /// </summary>
        public ClaimsPrincipal? UserPrincipal { get; set; }

        /// <summary>
        /// Authorization header gốc.
        /// BackgroundService dùng để tạo lại Request.Headers.Authorization.
        /// DapperExecutor có thể đang đọc token từ header này.
        /// </summary>
        public string AuthorizationHeader { get; set; } = string.Empty;
    }
}
