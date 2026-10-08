using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Controllers
{
    /// <summary>
    /// Controller cho màn hình khóa/mở sổ kỳ kế toán.
    /// Frontend nên dùng API overview để load màn hình.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/period-lock")]
    public class PeriodLockController : BaseApiController
    {
        private readonly IPeriodLockRepository _repo;
        private readonly ILogger<PeriodLockController> _logger;
        private readonly IPeriodLockJobQueue _periodLockJobQueue;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;

        public PeriodLockController(
            IPeriodLockRepository repo,
            ILogger<PeriodLockController> logger,
            IPeriodLockJobQueue periodLockJobQueue,
            ICompanyDatabaseResolver companyDatabaseResolver)
        {
            _repo = repo;
            _logger = logger;
            _periodLockJobQueue = periodLockJobQueue;
            _companyDatabaseResolver = companyDatabaseResolver;
        }

        /// <summary>
        /// Lấy dữ liệu tổng hợp cho màn hình khóa sổ.
        /// Frontend gọi:
        /// GET /api/period-lock/overview?year=2026
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview([FromQuery] int year)
        {
            if (year <= 0)
                return ValidationError("Year is required");

            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetOverviewAsync(companyCd, year);

            return Success(data);
        }

        /// <summary>
        /// API cũ: lấy riêng danh sách 12 tháng theo năm.
        /// Vẫn giữ để tương thích, nhưng frontend page nên dùng /overview.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPeriodLocks([FromQuery] int year)
        {
            if (year <= 0)
                return ValidationError("Year is required");

            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetPeriodLocksAsync(companyCd, year);

            return Success(data);
        }

        /// <summary>
        /// Tạo job khóa sổ.
        /// Backend tự tính FromPeriodYm thực tế dựa trên kỳ đã khóa hiện tại.
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartPeriodLock([FromBody] StartPeriodLockRequest? request)
        {
            if (request == null)
                return ValidationError("Request is required");

            if (request.Year <= 0)
                return ValidationError("Year is required");

            if (string.IsNullOrWhiteSpace(request.ToPeriodYm))
                return ValidationError("ToPeriodYm is required");

            var lockStepCodes = NormalizeLockStepCodes(request);

            if (lockStepCodes.Count == 0)
                return ValidationError("Steps are required");

            request.Steps = lockStepCodes;

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var databaseName = await _companyDatabaseResolver.ResolveDatabaseNameAsync(companyCd);

            request.FromPeriodYm = await _repo.ResolveLockFromPeriodYmAsync(
                companyCd,
                request,
                databaseName
            );

            if (string.Compare(request.FromPeriodYm, request.ToPeriodYm, StringComparison.Ordinal) > 0)
            {
                return ValidationError("Kỳ đích đã được khóa. Không còn kỳ nào cần xử lý.");
            }

            _repo.PopulateLockTargetMetadata(request);

            var jobId = CreateJobId(companyCd, PeriodLockJobType.Lock);
            var totalSteps = await _repo.CalculateLockTotalStepsAsync(
                companyCd,
                request,
                databaseName
            );

            var result = await _repo.CreateJobAsync(
                companyCd,
                jobId,
                request,
                totalSteps,
                userId
            );

            if (result <= 0)
                return ServerError("Create job failed");

            var backgroundUser = new ClaimsPrincipal(
                new ClaimsIdentity(
                    User.Claims.ToList(),
                    User.Identity?.AuthenticationType ?? "Background"
                )
            );

            var authorizationHeader = Request.Headers.Authorization.ToString();

            await _periodLockJobQueue.EnqueueAsync(new PeriodLockJobItem
            {
                JobType = PeriodLockJobType.Lock,
                CompanyCd = companyCd,
                JobId = jobId,
                UserId = userId,
                DatabaseName = databaseName,
                LockRequest = request,
                UserPrincipal = backgroundUser,
                AuthorizationHeader = authorizationHeader
            });

            return Created(new StartPeriodLockResponse
            {
                JobId = jobId,
                TotalSteps = totalSteps,
                FromPeriodYm = request.FromPeriodYm,
                TargetStepCode = request.Options?.TargetStepCode ?? string.Empty,
                TargetStepName = request.Options?.TargetStepName ?? string.Empty,
            }, "Job created successfully");
        }

        /// <summary>
        /// Tạo job mở sổ.
        /// Frontend gọi:
        /// POST /api/period-lock/unlock
        /// </summary>
        [HttpPost("unlock")]
        public async Task<IActionResult> StartPeriodUnlock([FromBody] StartPeriodUnlockRequest? request)
        {
            if (request == null)
                return ValidationError("Request is required");

            if (request.Year <= 0)
                return ValidationError("Year is required");

            if (string.IsNullOrWhiteSpace(request.FromPeriodYm))
                return ValidationError("FromPeriodYm is required");

            /*
             * Không bắt buộc ToPeriodYm khi mở sổ.
             * Frontend có thể đang hiển thị dữ liệu cũ nếu máy khác vừa khóa thêm kỳ.
             * Repository sẽ tự query DB để lấy kỳ khóa gần nhất thực tế trước khi chạy job.
             */
            if (string.IsNullOrWhiteSpace(request.ToPeriodYm))
                request.ToPeriodYm = request.FromPeriodYm;

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();

            var jobId = CreateJobId(companyCd, PeriodLockJobType.Unlock);
            var totalSteps = string.Compare(request.FromPeriodYm, request.ToPeriodYm, StringComparison.Ordinal) <= 0
                ? GetMonthCountInclusive(request.FromPeriodYm, request.ToPeriodYm)
                : 1;

            var result = await _repo.CreateUnlockJobAsync(
                companyCd,
                jobId,
                request,
                totalSteps,
                userId
            );

            if (result <= 0)
                return ServerError("Create unlock job failed");

            var backgroundUser = new ClaimsPrincipal(
                new ClaimsIdentity(
                    User.Claims.ToList(),
                    User.Identity?.AuthenticationType ?? "Background"
                )
            );

            var authorizationHeader = Request.Headers.Authorization.ToString();
            var databaseName = await _companyDatabaseResolver.ResolveDatabaseNameAsync(companyCd);

            await _periodLockJobQueue.EnqueueAsync(new PeriodLockJobItem
            {
                JobType = PeriodLockJobType.Unlock,
                CompanyCd = companyCd,
                JobId = jobId,
                UserId = userId,
                DatabaseName = databaseName,
                UnlockRequest = request,
                UserPrincipal = backgroundUser,
                AuthorizationHeader = authorizationHeader
            });

            return Created(new StartPeriodUnlockResponse
            {
                JobId = jobId
            }, "Unlock job created successfully");
        }

        /// <summary>
        /// Lấy progress của job khóa/mở sổ.
        /// Frontend gọi:
        /// GET /api/period-lock/jobs/{jobId}/progress
        /// </summary>
        [HttpGet("jobs/{jobId}/progress")]
        public async Task<IActionResult> GetJobProgress([FromRoute] string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return ValidationError("JobId is required");

            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetJobProgressAsync(companyCd, jobId);

            if (data == null)
                return ServerError("Job not found");

            return Success(data);
        }

        /// <summary>
        /// Tạo mã job duy nhất.
        /// </summary>
        private static string CreateJobId(string companyCd, string jobType)
        {
            var raw = $"{jobType}_{companyCd}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            return raw.Length <= 100 ? raw : raw.Substring(0, 100);
        }

        /// <summary>
        /// Chuẩn hóa step khóa sổ.
        /// - Nếu popup gửi TargetStepCode/TargetStepCodes thì hiểu là khóa tới bước đó.
        /// - Nếu flow cũ chỉ gửi Steps thì giữ đúng danh sách Steps.
        /// </summary>
        private static List<string> NormalizeLockStepCodes(StartPeriodLockRequest request)
        {
            var selectedStepCodes = (request.Steps ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(request.TargetStepCode) ||
                (request.TargetStepCodes != null && request.TargetStepCodes.Count > 0))
            {
                var rawStepCodes = request.TargetStepCodes != null && request.TargetStepCodes.Count > 0
                    ? request.TargetStepCodes
                    : new List<string> { request.TargetStepCode ?? string.Empty };

                var normalizedStepCodes = rawStepCodes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var invalidStepCode = normalizedStepCodes.FirstOrDefault(x => GetStepOrder(x) <= 0);
                if (!string.IsNullOrWhiteSpace(invalidStepCode))
                {
                    throw new InvalidOperationException($"Bước khóa sổ không hợp lệ: {invalidStepCode}");
                }

                var targetStepCode = normalizedStepCodes
                    .OrderByDescending(GetStepOrder)
                    .FirstOrDefault() ?? PeriodLockStepCode.ProfitLossReport;

                var expandedStepCodes = ExpandLockStepCodesToTarget(targetStepCode);

                return expandedStepCodes
                    .Where(stepCode => selectedStepCodes.Contains(stepCode, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(GetStepOrder)
                    .ToList();
            }

            return selectedStepCodes
                .Where(x => GetStepOrder(x) > 0)
                .OrderBy(GetStepOrder)
                .ToList();
        }

        private static List<string> ExpandLockStepCodesToTarget(string targetStepCode)
        {
            return targetStepCode switch
            {
                PeriodLockStepCode.FaPrepaidLock => new List<string>
                {
                    PeriodLockStepCode.FaPrepaidLock
                },

                PeriodLockStepCode.CogsSummary => new List<string>
                {
                    PeriodLockStepCode.FaPrepaidLock,
                    PeriodLockStepCode.CogsSummary
                },

                PeriodLockStepCode.ProfitLossReport => new List<string>
                {
                    PeriodLockStepCode.FaPrepaidLock,
                    PeriodLockStepCode.CogsSummary,
                    PeriodLockStepCode.ProfitLossReport
                },

                _ => throw new InvalidOperationException($"Bước khóa sổ không hợp lệ: {targetStepCode}")
            };
        }

        private static int GetStepOrder(string stepCode)
        {
            return stepCode switch
            {
                PeriodLockStepCode.FaPrepaidLock => 1,
                PeriodLockStepCode.CogsSummary => 2,
                PeriodLockStepCode.ProfitLossReport => 3,
                _ => 0
            };
        }

        /// <summary>
        /// Tính số tháng từ kỳ bắt đầu đến kỳ kết thúc.
        /// Ví dụ 202604 -> 202606 = 3 tháng.
        /// </summary>
        private static int GetMonthCountInclusive(string fromPeriodYm, string toPeriodYm)
        {
            var fromYear = int.Parse(fromPeriodYm.Substring(0, 4));
            var fromMonth = int.Parse(fromPeriodYm.Substring(4, 2));
            var toYear = int.Parse(toPeriodYm.Substring(0, 4));
            var toMonth = int.Parse(toPeriodYm.Substring(4, 2));

            var fromIndex = fromYear * 12 + fromMonth;
            var toIndex = toYear * 12 + toMonth;

            return Math.Max(toIndex - fromIndex + 1, 1);
        }
    }
}
