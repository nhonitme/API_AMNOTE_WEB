namespace API_AMNOTE_WEB.Models
{
    /// <summary>
    /// Trạng thái tổng của một tháng khóa sổ.
    /// Dùng cho acc_period_lock_month.STATUS.
    /// </summary>
    public static class PeriodLockMonthStatus
    {
        /// <summary>Tháng chưa xử lý bước khóa sổ nào.</summary>
        public const string Open = "OPEN";

        /// <summary>Tháng đã xử lý một phần, ví dụ chỉ chạy bước 1 hoặc bước 2.</summary>
        public const string Partial = "PARTIAL";

        /// <summary>Tháng đã hoàn tất toàn bộ các bước khóa sổ chính.</summary>
        public const string Locked = "LOCKED";

        /// <summary>Tháng đang được job xử lý.</summary>
        public const string Processing = "PROCESSING";

        /// <summary>Tháng có lỗi trong quá trình xử lý.</summary>
        public const string Error = "ERROR";
    }

    /// <summary>
    /// Trạng thái của step hoặc task con.
    /// Dùng cho acc_period_lock_step.STATUS và acc_period_lock_task.STATUS.
    /// </summary>
    public static class PeriodLockStepStatus
    {
        /// <summary>Chưa chạy.</summary>
        public const string Open = "OPEN";

        /// <summary>Đang chạy.</summary>
        public const string Processing = "PROCESSING";

        /// <summary>Đã hoàn tất thành công.</summary>
        public const string Done = "DONE";

        /// <summary>Chạy bị lỗi.</summary>
        public const string Error = "ERROR";
    }

    /// <summary>
    /// Trạng thái của job khóa/mở sổ.
    /// Dùng cho acc_period_lock_job.STATUS.
    /// </summary>
    public static class PeriodLockJobStatus
    {
        /// <summary>Job đang xử lý.</summary>
        public const string Running = "RUNNING";

        /// <summary>Job đã hoàn tất.</summary>
        public const string Done = "DONE";

        /// <summary>Job bị lỗi.</summary>
        public const string Error = "ERROR";
    }

    /// <summary>
    /// Loại job xử lý kỳ kế toán.
    /// </summary>
    public static class PeriodLockJobType
    {
        /// <summary>Job khóa sổ.</summary>
        public const string Lock = "LOCK";

        /// <summary>Job mở sổ.</summary>
        public const string Unlock = "UNLOCK";
    }

    /// <summary>
    /// Mã 3 bước chính của quy trình khóa sổ.
    /// </summary>
    public static class PeriodLockStepCode
    {
        /// <summary>Bước 1: Khóa tài sản cố định và chi phí trả trước.</summary>
        public const string FaPrepaidLock = "FA_PREPAID_LOCK";

        /// <summary>Bước 2: Báo cáo tổng hợp giá vốn.</summary>
        public const string CogsSummary = "COGS_SUMMARY";

        /// <summary>Bước 3: Báo cáo lãi lỗ.</summary>
        public const string ProfitLossReport = "PROFIT_LOSS_REPORT";
    }

    /// <summary>
    /// Mã các cách tự động chuyển dữ liệu giá vốn trong bước COGS_SUMMARY.
    /// </summary>
    public static class CogsTransferRuleCode
    {
        /// <summary>Chuyển dữ liệu từ tài khoản 154 sang 155.</summary>
        public const string Transfer154To155 = "154_TO_155";

        /// <summary>Chuyển dữ liệu từ tài khoản 154 sang 155 rồi sang 632.</summary>
        public const string Transfer154To155To632 = "154_TO_155_TO_632";

        /// <summary>Chuyển dữ liệu từ tài khoản 154 sang 632.</summary>
        public const string Transfer154To632 = "154_TO_632";
    }

    /// <summary>
    /// Cách tính bảng cân dùng cho báo cáo lãi lỗ.
    /// </summary>
    public static class ProfitLossBalanceMethod
    {
        /// <summary>Tính theo bảng cân đối tài khoản thông thường.</summary>
        public const string BalanceAccount = "BALANCE_ACCOUNT";

        /// <summary>Tính theo bảng cân đối tài khoản mẫu số dư 02 bên.</summary>
        public const string BalanceAccountTwoSide = "BALANCE_ACCOUNT_TWO_SIDE";
    }

    /// <summary>
    /// DTO biểu diễn trạng thái một step chính trong một tháng.
    /// Quy ước API dùng PascalCase để frontend không phải normalize nhiều kiểu tên.
    /// </summary>
    public sealed class PeriodLockStepDto
    {
        /// <summary>Mã step chính: FA_PREPAID_LOCK, COGS_SUMMARY, PROFIT_LOSS_REPORT.</summary>
        public string StepCode { get; set; } = string.Empty;

        /// <summary>Tên step hiển thị trên giao diện.</summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>Thứ tự step trong quy trình khóa sổ.</summary>
        public int StepOrder { get; set; }

        /// <summary>Trạng thái step: OPEN, PROCESSING, DONE, ERROR.</summary>
        public string Status { get; set; } = PeriodLockStepStatus.Open;

        /// <summary>Thời điểm bắt đầu chạy step, format yyyy-MM-dd HH:mm:ss.</summary>
        public string? StartedAt { get; set; }

        /// <summary>Thời điểm hoàn tất hoặc lỗi của step, format yyyy-MM-dd HH:mm:ss.</summary>
        public string? FinishedAt { get; set; }

        /// <summary>Ghi chú hoặc thông báo lỗi của step.</summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// DTO biểu diễn một tháng trên grid khóa sổ.
    /// Một dòng tương ứng một kỳ yyyyMM.
    /// </summary>
    public sealed class PeriodLockRowDto
    {
        /// <summary>Kỳ kế toán dạng yyyyMM. Ví dụ: 202606.</summary>
        public string PeriodYm { get; set; } = string.Empty;

        /// <summary>Nhãn kỳ hiển thị. Ví dụ: 06/2026.</summary>
        public string PeriodLabel { get; set; } = string.Empty;

        /// <summary>Ngày bắt đầu kỳ dạng yyyyMMdd.</summary>
        public string FromYmd { get; set; } = string.Empty;

        /// <summary>Ngày kết thúc kỳ dạng yyyyMMdd.</summary>
        public string ToYmd { get; set; } = string.Empty;

        /// <summary>Trạng thái tổng của tháng: OPEN, PARTIAL, LOCKED, PROCESSING, ERROR.</summary>
        public string Status { get; set; } = PeriodLockMonthStatus.Open;

        /// <summary>Thời điểm tháng được khóa hoàn tất, format yyyy-MM-dd HH:mm:ss.</summary>
        public string? LockedAt { get; set; }

        /// <summary>User đã khóa hoàn tất tháng.</summary>
        public string? LockedBy { get; set; }

        /// <summary>Ghi chú hoặc thông báo lỗi cấp tháng.</summary>
        public string? Message { get; set; }

        /// <summary>Danh sách 3 step chính của tháng.</summary>
        public List<PeriodLockStepDto> Steps { get; set; } = new();
    }

    /// <summary>
    /// DTO phẳng dùng riêng cho repository khi đọc result set step từ stored procedure.
    /// Có thêm PeriodYm để mapping step vào đúng tháng.
    /// </summary>
    public sealed class PeriodLockStepFlatDto
    {
        /// <summary>Kỳ kế toán yyyyMM mà step này thuộc về.</summary>
        public string PeriodYm { get; set; } = string.Empty;

        /// <summary>Mã step chính.</summary>
        public string StepCode { get; set; } = string.Empty;

        /// <summary>Tên step hiển thị.</summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>Thứ tự step.</summary>
        public int StepOrder { get; set; }

        /// <summary>Trạng thái step: OPEN, PROCESSING, DONE, ERROR.</summary>
        public string Status { get; set; } = PeriodLockStepStatus.Open;

        /// <summary>Thời điểm bắt đầu chạy step.</summary>
        public string? StartedAt { get; set; }

        /// <summary>Thời điểm hoàn tất hoặc lỗi của step.</summary>
        public string? FinishedAt { get; set; }

        /// <summary>Ghi chú hoặc thông báo lỗi của step.</summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// DTO tổng hợp cho màn hình khóa sổ.
    /// Frontend chỉ cần gọi một API overview để render page.
    /// </summary>
    public sealed class PeriodLockOverviewDto
    {
        /// <summary>Ngày tháng năm đầu kỳ kế toán của công ty dạng yyyyMMdd.</summary>
        public int FiscalStartYmd { get; set; }

        /// <summary>Kỳ đầu tiên được phép khóa sổ, suy ra từ FiscalStartYmd, dạng yyyyMM.</summary>
        public string FiscalStartPeriodYm { get; set; } = string.Empty;

        /// <summary>Danh sách năm combobox, suy ra từ FiscalStartYmd đến năm hiện tại.</summary>
        public List<int> YearList { get; set; } = new();

        /// <summary>Kỳ đã khóa hiện tại của công ty dạng yyyyMM. Rỗng nếu chưa khóa kỳ nào.</summary>
        public string CurrentLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Nhãn kỳ đã khóa hiện tại. Ví dụ: 05/2025.</summary>
        public string CurrentLockedPeriodLabel { get; set; } = "Chưa khóa kỳ nào";

        /// <summary>Kỳ mới nhất đã DONE bước 1 - TSCĐ / CP trả trước.</summary>
        public string FaPrepaidLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ mới nhất đã DONE bước 2 - Tổng hợp giá vốn.</summary>
        public string CogsSummaryLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ mới nhất đã DONE bước 3 - Báo cáo lãi lỗ.</summary>
        public string ProfitLossLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ mới nhất còn bất kỳ step DONE nào. Dùng cho mở sổ.</summary>
        public string AnyStepLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Danh sách 12 tháng của năm đang xem.</summary>
        public List<PeriodLockRowDto> Rows { get; set; } = new();
    }

    /// <summary>
    /// DTO trả về kỳ đã khóa hiện tại của công ty.
    /// Dùng nội bộ trong repository để tạo PeriodLockOverviewDto.
    /// </summary>
    public sealed class PeriodLockCurrentStatusDto
    {
        /// <summary>Kỳ đã khóa sau cùng dạng yyyyMM.</summary>
        public string CurrentLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Nhãn kỳ đã khóa sau cùng.</summary>
        public string CurrentLockedPeriodLabel { get; set; } = "";

        /// <summary>Kỳ mới nhất đã DONE bước 1 - TSCĐ / CP trả trước.</summary>
        public string FaPrepaidLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ mới nhất đã DONE bước 2 - Tổng hợp giá vốn.</summary>
        public string CogsSummaryLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ mới nhất đã DONE bước 3 - Báo cáo lãi lỗ.</summary>
        public string ProfitLossLockedPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ mới nhất còn bất kỳ step DONE nào. Dùng cho mở sổ.</summary>
        public string AnyStepLockedPeriodYm { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request frontend gửi khi bấm nút Khóa sổ đến đây.
    /// Backend sẽ tự tính FromPeriodYm thực tế dựa trên kỳ đã khóa hiện tại.
    /// </summary>
    public sealed class StartPeriodLockRequest
    {
        /// <summary>Năm đang được chọn trên combobox để xem dữ liệu.</summary>
        public int Year { get; set; }

        /// <summary>Kỳ bắt đầu xử lý dạng yyyyMM. Backend có thể ghi đè giá trị này.</summary>
        public string FromPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ đích người dùng muốn khóa tới dạng yyyyMM.</summary>
        public string ToPeriodYm { get; set; } = string.Empty;

        /// <summary>Danh sách step chính người dùng chọn. Với flow cũ, backend chạy đúng danh sách này.</summary>
        public List<string> Steps { get; set; } = new();

        /// <summary>
        /// Bước đích muốn khóa tới khi thao tác từ popup từng bước.
        /// Ví dụ:
        /// FA_PREPAID_LOCK = khóa tới bước 1.
        /// COGS_SUMMARY = tự khóa bước 1 nếu cần, sau đó khóa bước 2.
        /// PROFIT_LOSS_REPORT = tự khóa bước 1, bước 2 nếu cần, sau đó khóa bước 3.
        /// </summary>
        public string? TargetStepCode { get; set; }

        /// <summary>
        /// Tương thích frontend: nếu truyền nhiều bước, backend hiểu là khóa tới bước lớn nhất trong danh sách.
        /// Ví dụ [COGS_SUMMARY] hoặc [FA_PREPAID_LOCK, COGS_SUMMARY] đều hiểu là khóa tới bước 2.
        /// </summary>
        public List<string>? TargetStepCodes { get; set; }

        /// <summary>Options chi tiết cho từng step.</summary>
        public PeriodLockOptionsDto? Options { get; set; }
    }

    /// <summary>
    /// Request frontend gửi khi bấm nút Mở sổ từ đây.
    /// </summary>
    public sealed class StartPeriodUnlockRequest
    {
        /// <summary>Năm tài chính đang chọn trên màn hình.</summary>
        public int Year { get; set; }

        /// <summary>Kỳ bắt đầu mở sổ dạng yyyyMM.</summary>
        public string FromPeriodYm { get; set; } = string.Empty;

        /// <summary>Kỳ kết thúc mở sổ dạng yyyyMM.</summary>
        public string ToPeriodYm { get; set; } = string.Empty;

        /// <summary>Lý do mở sổ để lưu audit/log.</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Bước đích muốn mở tới.
        /// Ví dụ:
        /// PROFIT_LOSS_REPORT = mở tới bước 3.
        /// COGS_SUMMARY = tự mở bước 3 rồi bước 2.
        /// FA_PREPAID_LOCK = tự mở bước 3, bước 2 rồi bước 1.
        /// </summary>
        public string? TargetStepCode { get; set; }

        /// <summary>
        /// Tương thích frontend cũ/mới: nếu truyền nhiều bước, backend sẽ hiểu là mở tới bước nhỏ nhất trong danh sách.
        /// Ví dụ [PROFIT_LOSS_REPORT, COGS_SUMMARY] vẫn hiểu là mở tới COGS_SUMMARY.
        /// </summary>
        public List<string>? TargetStepCodes { get; set; }
    }

    /// <summary>
    /// Options tổng hợp cho job khóa sổ.
    /// </summary>
    public sealed class PeriodLockOptionsDto
    {
        /// <summary>Options cho step COGS_SUMMARY.</summary>
        public CogsSummaryOptionsDto? CogsSummary { get; set; }

        /// <summary>Options cho step PROFIT_LOSS_REPORT.</summary>
        public ProfitLossReportOptionsDto? ProfitLossReport { get; set; }

        /// <summary>Bước đích user chọn (popup hoặc bước cao nhất). Lưu trong OPTIONS_JSON của job.</summary>
        public string? TargetStepCode { get; set; }

        /// <summary>Tên hiển thị bước đích trên progress.</summary>
        public string? TargetStepName { get; set; }
    }

    /// <summary>
    /// Options cho bước báo cáo tổng hợp giá vốn.
    /// </summary>
    public sealed class CogsSummaryOptionsDto
    {
        /// <summary>Danh sách cách tự động chuyển dữ liệu giá vốn.</summary>
        public string TransferRules { get; set; } = CogsTransferRuleCode.Transfer154To632;
    }

    /// <summary>
    /// Options cho bước báo cáo lãi lỗ.
    /// </summary>
    public sealed class ProfitLossReportOptionsDto
    {
        /// <summary>Cách tính bảng cân trước khi lập báo cáo lãi lỗ.</summary>
        public string BalanceMethod { get; set; } = ProfitLossBalanceMethod.BalanceAccountTwoSide;
    }

    /// <summary>
    /// Response sau khi tạo job thành công.
    /// </summary>
    public sealed class StartPeriodLockResponse
    {
        /// <summary>Mã job để frontend polling progress.</summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>Tổng task thực tế (chỉ tính bước chưa DONE trong phạm vi kỳ).</summary>
        public int TotalSteps { get; set; }

        /// <summary>Kỳ bắt đầu thực tế backend đã resolve.</summary>
        public string FromPeriodYm { get; set; } = string.Empty;

        /// <summary>Mã bước đích user chọn.</summary>
        public string TargetStepCode { get; set; } = string.Empty;

        /// <summary>Tên bước đích hiển thị trên progress.</summary>
        public string TargetStepName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response sau khi tạo job mở sổ thành công.
    /// </summary>
    public sealed class StartPeriodUnlockResponse
    {
        /// <summary>Mã job để frontend polling progress.</summary>
        public string JobId { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO trả về tiến độ xử lý job cho frontend.
    /// </summary>
    public sealed class PeriodLockProgressDto
    {
        /// <summary>Mã job đang theo dõi.</summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>Phần trăm tiến độ xử lý, từ 0 đến 100.</summary>
        public decimal Percent { get; set; }

        /// <summary>Tổng số task cần xử lý.</summary>
        public int TotalSteps { get; set; }

        /// <summary>Số task đã xử lý xong.</summary>
        public int DoneSteps { get; set; }

        /// <summary>Kỳ hiện tại đang xử lý dạng yyyyMM.</summary>
        public string CurrentPeriodYm { get; set; } = string.Empty;

        /// <summary>Nhãn kỳ hiện tại đang xử lý.</summary>
        public string CurrentPeriodLabel { get; set; } = "-";

        /// <summary>Mã step hiện tại đang xử lý.</summary>
        public string CurrentStepCode { get; set; } = string.Empty;

        /// <summary>Tên step hoặc task hiện tại đang xử lý.</summary>
        public string CurrentStepName { get; set; } = "-";

        /// <summary>Mã bước đích user chọn khi tạo job.</summary>
        public string TargetStepCode { get; set; } = string.Empty;

        /// <summary>Tên bước đích user chọn khi tạo job.</summary>
        public string TargetStepName { get; set; } = string.Empty;

        /// <summary>Thông báo tiến độ hiện tại.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Trạng thái job: RUNNING, DONE, ERROR.</summary>
        public string Status { get; set; } = PeriodLockJobStatus.Running;
    }

    public sealed class PeriodLockFiscalStartYmdResult
    {
        /// <summary>Ngày đầu kỳ kế toán dạng yyyyMMdd. Dùng string để nhận an toàn cả CHAR/VARCHAR/INT từ MySQL.</summary>
        public string? FiscalStartYmd { get; set; }
    }

    public sealed class PeriodLockStatusResult
    {
        public string Status { get; set; } = PeriodLockMonthStatus.Open;
    }

    public sealed class PeriodLockJobRequestDbRow
    {
        public int YearValue { get; set; }
        public string? FromPeriodYm { get; set; }
        public string? ToPeriodYm { get; set; }
        public string? StepsText { get; set; }
        public string? OptionsJson { get; set; }
    }
}
