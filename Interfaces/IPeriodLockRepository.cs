using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    /// <summary>
    /// Repository cho chức năng khóa/mở sổ kỳ kế toán.
    /// </summary>
    public interface IPeriodLockRepository
    {
        /// <summary>
        /// Lấy toàn bộ dữ liệu cần thiết cho màn hình khóa sổ.
        /// Đây là API read-model chính cho page, gồm năm đầu kỳ, danh sách năm, kỳ đã khóa hiện tại và rows của năm đang xem.
        /// </summary>
        Task<PeriodLockOverviewDto> GetOverviewAsync(string companyCd, int year);

        /// <summary>
        /// Lấy danh sách 12 tháng và trạng thái step của năm đang xem.
        /// </summary>
        Task<IEnumerable<PeriodLockRowDto>> GetPeriodLocksAsync(string companyCd, int year);

        /// <summary>
        /// Backend tự xác định kỳ bắt đầu khóa thực tế.
        /// Nếu chưa khóa kỳ nào thì lấy FiscalStartYear + '01'.
        /// Nếu đã khóa thì lấy tháng kế tiếp kỳ LOCKED gần nhất.
        /// </summary>
        Task<string> GetActualLockFromPeriodYmAsync(string companyCd, string toPeriodYm);

        /// <summary>
        /// Resolve FromPeriodYm thực tế theo flow thường hoặc popup khóa tới bước.
        /// </summary>
        Task<string> ResolveLockFromPeriodYmAsync(
            string companyCd,
            StartPeriodLockRequest request,
            string? databaseName = null);

        /// <summary>
        /// Tính tổng progress units chỉ cho các bước chưa DONE trong phạm vi kỳ.
        /// </summary>
        Task<int> CalculateLockTotalStepsAsync(
            string companyCd,
            StartPeriodLockRequest request,
            string? databaseName = null);

        /// <summary>
        /// Ghi metadata bước đích vào request.Options trước khi tạo job.
        /// </summary>
        void PopulateLockTargetMetadata(StartPeriodLockRequest request);

        /// <summary>
        /// Tạo job khóa/mở sổ.
        /// </summary>
        Task<int> CreateJobAsync(string companyCd, string jobId, StartPeriodLockRequest request, int totalSteps, string userId);

        /// <summary>
        /// Tạo job mở sổ.
        /// </summary>
        Task<int> CreateUnlockJobAsync(string companyCd, string jobId, StartPeriodUnlockRequest request, int totalSteps, string userId);

        /// <summary>
        /// Lấy request gốc của job từ DB để phục vụ retry/resume.
        /// Job vừa tạo nên truyền request trực tiếp, không cần gọi lại hàm này.
        /// </summary>
        Task<StartPeriodLockRequest?> GetJobRequestAsync(string companyCd, string jobId, string? databaseName = null);

        /// <summary>
        /// Lấy tiến độ job hiện tại để frontend polling.
        /// </summary>
        Task<PeriodLockProgressDto?> GetJobProgressAsync(string companyCd, string jobId);

        /// <summary>
        /// Cập nhật progress của job.
        /// </summary>
        Task<int> UpdateJobProgressAsync(
            string companyCd,
            string jobId,
            int doneSteps,
            int totalSteps,
            string currentPeriodYm,
            string currentStepCode,
            string currentStepName,
            string message,
            string status,
            string? databaseName = null);

        /// <summary>
        /// Insert/update trạng thái tổng của một tháng.
        /// </summary>
        Task<int> UpsertMonthAsync(
            string companyCd,
            string periodYm,
            string fromYmd,
            string toYmd,
            string status,
            string? message,
            string userId,
            string? databaseName = null);

        /// <summary>
        /// Insert/update trạng thái một bước chính trong tháng.
        /// </summary>
        Task<int> UpsertStepAsync(
            string companyCd,
            string periodYm,
            string stepCode,
            string stepName,
            int stepOrder,
            string status,
            string? message,
            string userId,
            string? databaseName = null);

        /// <summary>
        /// Insert/update trạng thái task con trong step.
        /// </summary>
        Task<int> UpsertTaskAsync(
            string companyCd,
            string periodYm,
            string stepCode,
            string taskCode,
            string taskName,
            int taskOrder,
            string status,
            string? message,
            string userId,
            string? databaseName = null);

        /// <summary>
        /// Ghi log xử lý từng task.
        /// </summary>
        Task<int> AddJobLogAsync(
            string companyCd,
            string jobId,
            string periodYm,
            string stepCode,
            string taskCode,
            string taskName,
            string status,
            string? message,
            string userId);

        /// <summary>
        /// Tính trạng thái tổng của tháng dựa vào trạng thái các step.
        /// </summary>
        Task<string> DeriveMonthStatusAsync(string companyCd, string periodYm, string? databaseName = null);

        /// <summary>
        /// Chạy job khóa sổ bằng request có sẵn.
        /// </summary>
        Task RunJobAsync(
            string companyCd,
            string jobId,
            StartPeriodLockRequest request,
            string userId,
            string databaseName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Chạy job khóa sổ bằng cách đọc request từ DB. Chỉ dùng cho retry/resume.
        /// </summary>
        Task RunJobAsync(
            string companyCd,
            string jobId,
            string userId,
            string databaseName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Chạy job mở sổ.
        /// </summary>
        Task RunUnlockJobAsync(
            string companyCd,
            string jobId,
            StartPeriodUnlockRequest request,
            string userId,
            string databaseName,
            CancellationToken cancellationToken);
    }
}
