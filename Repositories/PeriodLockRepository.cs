using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    /// <summary>
    /// Repository xử lý DB cho khóa/mở sổ kỳ kế toán.
    /// SQL nghiệp vụ nằm trong stored procedure, C# chỉ gọi CALL theo style AMNOTE hiện tại.
    /// </summary>
    public class PeriodLockRepository : IPeriodLockRepository
    {
        private readonly DapperExecutor _db;

        public PeriodLockRepository(DapperExecutor db)
        {
            _db = db;
        }

        /// <summary>
        /// API read-model tổng hợp cho màn hình khóa sổ.
        /// Frontend chỉ cần gọi một API overview để có đủ dữ liệu render page.
        /// </summary>
        public async Task<PeriodLockOverviewDto> GetOverviewAsync(string companyCd, int year)
        {
            // FiscalStartYmd lấy từ sp_period_lock_fiscal_start_year_get.
            // Store trả CARRYFORWARD_YMD dạng yyyyMMdd, ví dụ 20250101.
            var fiscalStartYmd = await GetFiscalStartYmdAsync(companyCd);
            var startYear = GetYearFromYmd(fiscalStartYmd);
            var currentStatus = await GetCurrentStatusAsync(companyCd);
            var rows = (await GetPeriodLocksAsync(companyCd, year)).ToList();

            var currentYear = DateTime.Now.Year;
            if (startYear <= 0 || startYear > currentYear)
            {
                fiscalStartYmd = BuildDefaultFiscalStartYmd(currentYear);
                startYear = currentYear;
            }

            var yearList = Enumerable
                .Range(startYear, currentYear - startYear + 1)
                .ToList();

            return new PeriodLockOverviewDto
            {
                FiscalStartYmd = fiscalStartYmd,
                FiscalStartPeriodYm = GetFiscalStartPeriodYm(fiscalStartYmd),
                YearList = yearList,
                CurrentLockedPeriodYm = currentStatus?.CurrentLockedPeriodYm ?? string.Empty,
                CurrentLockedPeriodLabel = currentStatus?.CurrentLockedPeriodLabel ?? "",
                FaPrepaidLockedPeriodYm = currentStatus?.FaPrepaidLockedPeriodYm ?? string.Empty,
                CogsSummaryLockedPeriodYm = currentStatus?.CogsSummaryLockedPeriodYm ?? string.Empty,
                ProfitLossLockedPeriodYm = currentStatus?.ProfitLossLockedPeriodYm ?? string.Empty,
                AnyStepLockedPeriodYm = currentStatus?.AnyStepLockedPeriodYm ?? string.Empty,
                Rows = rows
            };
        }

        /// <summary>
        /// Lấy danh sách 12 tháng và map danh sách step vào từng tháng.
        /// </summary>
        public async Task<IEnumerable<PeriodLockRowDto>> GetPeriodLocksAsync(string companyCd, int year)
        {
            const string monthQuery = "CALL sp_period_lock_months_get(@p_COMPANY_CD, @p_YEAR)";
            const string stepQuery = "CALL sp_period_lock_steps_get(@p_COMPANY_CD, @p_YEAR)";

            var months = (await _db.QueryAsync<PeriodLockRowDto>(
                Net_DB.Net_DB_Company,
                monthQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_YEAR = year
                }
            )).ToList();

            var steps = (await _db.QueryAsync<PeriodLockStepFlatDto>(
                Net_DB.Net_DB_Company,
                stepQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_YEAR = year
                }
            )).ToList();

            var stepMap = steps
                .GroupBy(x => x.PeriodYm)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new PeriodLockStepDto
                    {
                        StepCode = x.StepCode,
                        StepName = x.StepName,
                        StepOrder = x.StepOrder,
                        Status = x.Status,
                        StartedAt = x.StartedAt,
                        FinishedAt = x.FinishedAt,
                        Message = x.Message
                    }).ToList()
                );

            foreach (var month in months)
            {
                if (stepMap.TryGetValue(month.PeriodYm, out var monthSteps))
                {
                    month.Steps = monthSteps;
                }
            }

            return months;
        }

        /// <summary>
        /// Lấy ngày đầu kỳ kế toán của công ty từ sp_period_lock_fiscal_start_year_get.
        /// Lưu ý: store trả CARRYFORWARD_YMD dạng yyyyMMdd, không phải chỉ yyyy.
        /// Backend luôn lấy giá trị này từ DB, không dựa vào frontend.
        /// </summary>
        public async Task<int> GetFiscalStartYmdAsync(string companyCd, string? sDBName = null)
        {
            const string query = "CALL sp_period_lock_fiscal_start_year_get(@p_COMPANY_CD)";

            var rows = await _db.QueryAsync<PeriodLockFiscalStartYmdResult>(
                Net_DB.Net_DB_Company,
                query,
                new { p_COMPANY_CD = companyCd },
                sDBName
            );

            var row = rows.FirstOrDefault();
            return NormalizeFiscalStartYmd(row?.FiscalStartYmd);
        }

        private static int NormalizeFiscalStartYmd(string? fiscalStartValue)
        {
            var digits = new string((fiscalStartValue ?? string.Empty).Where(char.IsDigit).ToArray());

            if (digits.Length >= 8)
            {
                digits = digits.Substring(0, 8);

                if (DateTime.TryParseExact(
                        digits,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _))
                {
                    return int.Parse(digits);
                }
            }

            return BuildDefaultFiscalStartYmd(DateTime.Now.Year);
        }

        private static int BuildDefaultFiscalStartYmd(int year)
        {
            return int.Parse($"{year}0101");
        }

        private static int GetYearFromYmd(int fiscalStartYmd)
        {
            var text = fiscalStartYmd.ToString().PadLeft(8, '0');
            return int.Parse(text.Substring(0, 4));
        }

        private static string GetFiscalStartPeriodYm(int fiscalStartYmd)
        {
            var text = fiscalStartYmd.ToString().PadLeft(8, '0');
            return text.Substring(0, 6);
        }

        /// <summary>
        /// Lấy kỳ đã khóa hiện tại của công ty.
        /// Method này dùng nội bộ cho overview, không cần expose API riêng.
        /// </summary>
        private async Task<PeriodLockCurrentStatusDto?> GetCurrentStatusAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL sp_period_lock_current_status_get(@p_COMPANY_CD)";

            var rows = await _db.QueryAsync<PeriodLockCurrentStatusDto>(
                Net_DB.Net_DB_Company,
                query,
                new { p_COMPANY_CD = companyCd },
                databaseName
            );

            return rows.FirstOrDefault();
        }

        /// <summary>
        /// Backend tự xác định kỳ bắt đầu khóa thực tế, không tin FromPeriodYm từ frontend.
        /// </summary>
        public async Task<string> GetActualLockFromPeriodYmAsync(
            string companyCd,
            string toPeriodYm)
        {
            /*
             * Flow khóa sổ tổng quát vẫn dựa trên kỳ đã LOCKED hoàn toàn.
             * Nguồn xác định kỳ không đọc trực tiếp nhiều bảng ở C# nữa, mà lấy từ
             * sp_period_lock_current_status_get để đồng bộ với API overview.
             */
            var currentStatus = await GetCurrentStatusAsync(companyCd);
            var currentLockedPeriodYm = currentStatus?.CurrentLockedPeriodYm ?? string.Empty;

            var fiscalStartYmd = await GetFiscalStartYmdAsync(companyCd);
            var fiscalStartPeriodYm = GetFiscalStartPeriodYm(fiscalStartYmd);

            if (string.IsNullOrWhiteSpace(currentLockedPeriodYm))
            {
                return fiscalStartPeriodYm;
            }

            if (string.Compare(currentLockedPeriodYm, toPeriodYm, StringComparison.Ordinal) >= 0)
            {
                return AddMonthsToPeriodYm(toPeriodYm, 1);
            }

            return AddMonthsToPeriodYm(currentLockedPeriodYm, 1);
        }

        public async Task<string> ResolveLockFromPeriodYmAsync(
            string companyCd,
            StartPeriodLockRequest request,
            string? databaseName = null)
        {
            var lockStepCodes = NormalizeLockStepCodes(request);

            if (IsPopupLockStepRequest(request))
            {
                return await GetActualLockFromPeriodYmByStepsAsync(
                    companyCd,
                    request.ToPeriodYm,
                    lockStepCodes,
                    databaseName
                );
            }

            if (!string.IsNullOrWhiteSpace(request.FromPeriodYm))
            {
                return request.FromPeriodYm;
            }

            return await GetActualLockFromPeriodYmAsync(companyCd, request.ToPeriodYm);
        }

        public async Task<int> CalculateLockTotalStepsAsync(
            string companyCd,
            StartPeriodLockRequest request,
            string? databaseName = null)
        {
            var lockStepCodes = NormalizeLockStepCodes(request);
            var fromPeriodYm = await ResolveLockFromPeriodYmAsync(companyCd, request, databaseName);
            var toPeriodYm = request.ToPeriodYm;

            if (string.Compare(fromPeriodYm, toPeriodYm, StringComparison.Ordinal) > 0)
            {
                return 1;
            }

            var totalSteps = 0;

            foreach (var periodYm in GetPeriodRange(fromPeriodYm, toPeriodYm))
            {
                foreach (var stepCode in lockStepCodes)
                {
                    if (!await IsStepDoneAsync(companyCd, periodYm, stepCode, databaseName))
                    {
                        totalSteps += PeriodLockProgressUnit.GetStepUnitCount(stepCode);
                    }
                }
            }

            return Math.Max(totalSteps, 1);
        }

        public void PopulateLockTargetMetadata(StartPeriodLockRequest request)
        {
            var targetStepCode = ResolveTargetStepCode(request);
            request.Options ??= new PeriodLockOptionsDto();

            if (string.IsNullOrWhiteSpace(targetStepCode))
            {
                return;
            }

            request.Options.TargetStepCode = targetStepCode;
            request.Options.TargetStepName = GetStepName(targetStepCode);
            request.TargetStepCode ??= targetStepCode;
        }

        /// <summary>
        /// Xác định kỳ bắt đầu khóa thực tế theo từng step được chọn.
        /// Dùng cho popup "Khóa tới bước".
        ///
        /// Nguyên tắc:
        /// - Khóa bước 1: chạy từ tháng sau kỳ gần nhất đã DONE bước 1.
        /// - Khóa bước 2: chạy từ tháng đầu tiên còn thiếu trong nhóm bước 1/2.
        /// - Khóa bước 3: chạy từ tháng đầu tiên còn thiếu trong nhóm bước 1/2/3.
        ///
        /// Nhờ vậy nếu bước 1 đã khóa tới 08/2026, bước 2 mới khóa tới 05/2026,
        /// khi khóa tới bước 2 hệ thống sẽ chạy từ 06/2026 -> 08/2026,
        /// không chạy sai riêng tháng user đang bấm.
        /// </summary>
        private async Task<string> GetActualLockFromPeriodYmByStepsAsync(
            string companyCd,
            string toPeriodYm,
            List<string> stepCodes,
            string? databaseName = null)
        {
            /*
             * Flow popup khóa từng bước:
             * - Không dùng FromPeriodYm từ frontend.
             * - Không query từng bảng rời trong C#.
             * - Lấy toàn bộ mốc kỳ hiện tại từ sp_period_lock_current_status_get.
             *
             * Ví dụ:
             * - FA_PREPAID_LOCK đã DONE tới 08/2026
             * - COGS_SUMMARY đã DONE tới 05/2026
             * - User khóa tới bước 2, target 08/2026
             * => chạy từ 06/2026 đến 08/2026.
             */
            var fiscalStartYmd = await GetFiscalStartYmdAsync(companyCd, databaseName);
            var fiscalStartPeriodYm = GetFiscalStartPeriodYm(fiscalStartYmd);

            if (stepCodes == null || stepCodes.Count == 0)
            {
                return fiscalStartPeriodYm;
            }

            var currentStatus = await GetCurrentStatusAsync(companyCd, databaseName);
            var actualFromPeriodYm = AddMonthsToPeriodYm(toPeriodYm, 1);

            foreach (var stepCode in stepCodes
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var latestDonePeriodYm = GetLockedPeriodYmFromCurrentStatus(currentStatus, stepCode);

                var candidateFromPeriodYm = string.IsNullOrWhiteSpace(latestDonePeriodYm)
                    ? fiscalStartPeriodYm
                    : string.Compare(latestDonePeriodYm, toPeriodYm, StringComparison.Ordinal) >= 0
                        ? AddMonthsToPeriodYm(toPeriodYm, 1)
                        : AddMonthsToPeriodYm(latestDonePeriodYm, 1);

                if (string.Compare(candidateFromPeriodYm, fiscalStartPeriodYm, StringComparison.Ordinal) < 0)
                {
                    candidateFromPeriodYm = fiscalStartPeriodYm;
                }

                if (string.Compare(candidateFromPeriodYm, actualFromPeriodYm, StringComparison.Ordinal) < 0)
                {
                    actualFromPeriodYm = candidateFromPeriodYm;
                }
            }

            return actualFromPeriodYm;
        }

        private static string GetLockedPeriodYmFromCurrentStatus(
            PeriodLockCurrentStatusDto? currentStatus,
            string stepCode)
        {
            if (currentStatus == null)
            {
                return string.Empty;
            }

            return stepCode switch
            {
                PeriodLockStepCode.FaPrepaidLock => currentStatus.FaPrepaidLockedPeriodYm ?? string.Empty,
                PeriodLockStepCode.CogsSummary => currentStatus.CogsSummaryLockedPeriodYm ?? string.Empty,
                PeriodLockStepCode.ProfitLossReport => currentStatus.ProfitLossLockedPeriodYm ?? string.Empty,
                _ => string.Empty
            };
        }

        /// <summary>
        /// Tạo job khóa sổ.
        /// </summary>
        public async Task<int> CreateJobAsync(
            string companyCd,
            string jobId,
            StartPeriodLockRequest request,
            int totalSteps,
            string userId)
        {
            const string proc = @"
                CALL sp_period_lock_job_create(
                    @p_COMPANY_CD,
                    @p_JOB_ID,
                    @p_YEAR_VALUE,
                    @p_FROM_PERIOD_YM,
                    @p_TO_PERIOD_YM,
                    @p_STEPS_TEXT,
                    @p_OPTIONS_JSON,
                    @p_TOTAL_STEPS,
                    @p_USER_ID
                );";

            var param = new
            {
                p_COMPANY_CD = companyCd,
                p_JOB_ID = jobId,
                p_YEAR_VALUE = request.Year,
                p_FROM_PERIOD_YM = request.FromPeriodYm,
                p_TO_PERIOD_YM = request.ToPeriodYm,
                p_STEPS_TEXT = string.Join(",", request.Steps ?? new List<string>()),
                p_OPTIONS_JSON = JsonSerializer.Serialize(request.Options ?? new PeriodLockOptionsDto()),
                p_TOTAL_STEPS = totalSteps,
                p_USER_ID = userId
            };

            return await ExecuteAsync(proc, param);
        }

        /// <summary>
        /// Tạo job mở sổ. Dùng chung bảng acc_period_lock_job.
        /// </summary>
        public async Task<int> CreateUnlockJobAsync(
            string companyCd,
            string jobId,
            StartPeriodUnlockRequest request,
            int totalSteps,
            string userId)
        {
            const string proc = @"
                CALL sp_period_lock_unlock_job_create(
                    @p_COMPANY_CD,
                    @p_JOB_ID,
                    @p_YEAR_VALUE,
                    @p_FROM_PERIOD_YM,
                    @p_TO_PERIOD_YM,
                    @p_REASON,
                    @p_TOTAL_STEPS,
                    @p_USER_ID
                );";

            var param = new
            {
                p_COMPANY_CD = companyCd,
                p_JOB_ID = jobId,
                p_YEAR_VALUE = request.Year,
                p_FROM_PERIOD_YM = request.FromPeriodYm,
                p_TO_PERIOD_YM = request.ToPeriodYm,
                p_REASON = request.Reason ?? string.Empty,
                p_TOTAL_STEPS = totalSteps,
                p_USER_ID = userId
            };

            return await ExecuteAsync(proc, param);
        }

        /// <summary>
        /// Lấy request gốc của job. Chỉ dùng cho retry/resume, không dùng cho job vừa tạo.
        /// </summary>
        public async Task<StartPeriodLockRequest?> GetJobRequestAsync(string companyCd, string jobId, string? databaseName = null)
        {
            const string query = "CALL sp_period_lock_job_get_request(@p_COMPANY_CD, @p_JOB_ID)";

            var rows = await _db.QueryAsync<PeriodLockJobRequestDbRow>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_JOB_ID = jobId
                },
                databaseName
            );

            var row = rows.FirstOrDefault();
            if (row == null) return null;

            var request = new StartPeriodLockRequest
            {
                Year = row.YearValue,
                FromPeriodYm = row.FromPeriodYm ?? string.Empty,
                ToPeriodYm = row.ToPeriodYm ?? string.Empty,
                Steps = (row.StepsText ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .ToList(),
                Options = new PeriodLockOptionsDto()
            };

            if (!string.IsNullOrWhiteSpace(row.OptionsJson))
            {
                request.Options = JsonSerializer.Deserialize<PeriodLockOptionsDto>(
                    row.OptionsJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                ) ?? new PeriodLockOptionsDto();
            }

            request.TargetStepCode = request.Options?.TargetStepCode;

            return request;
        }

        /// <summary>
        /// Lấy progress hiện tại của job.
        /// </summary>
        public async Task<PeriodLockProgressDto?> GetJobProgressAsync(string companyCd, string jobId)
        {
            const string query = "CALL sp_period_lock_job_get_progress(@p_COMPANY_CD, @p_JOB_ID)";

            var rows = await _db.QueryAsync<PeriodLockProgressDto>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_JOB_ID = jobId
                }
            );

            var progress = rows.FirstOrDefault();
            if (progress == null)
            {
                return null;
            }

            var request = await GetJobRequestAsync(companyCd, jobId);
            if (request?.Options != null)
            {
                progress.TargetStepCode = request.Options.TargetStepCode ?? request.TargetStepCode ?? string.Empty;
                progress.TargetStepName = request.Options.TargetStepName
                    ?? GetStepName(progress.TargetStepCode);
            }

            return progress;
        }

        /// <summary>
        /// Cập nhật progress job. Store tự tính Percent.
        /// </summary>
        public async Task<int> UpdateJobProgressAsync(
            string companyCd,
            string jobId,
            int doneSteps,
            int totalSteps,
            string currentPeriodYm,
            string currentStepCode,
            string currentStepName,
            string message,
            string status,
            string? databaseName = null)
        {
            const string proc = @"
                CALL sp_period_lock_job_update_progress(
                    @p_COMPANY_CD,
                    @p_JOB_ID,
                    @p_DONE_STEPS,
                    @p_TOTAL_STEPS,
                    @p_CURRENT_PERIOD_YM,
                    @p_CURRENT_STEP_CODE,
                    @p_CURRENT_STEP_NAME,
                    @p_MESSAGE,
                    @p_STATUS
                );";

            var param = new
            {
                p_COMPANY_CD = companyCd,
                p_JOB_ID = jobId,
                p_DONE_STEPS = doneSteps,
                p_TOTAL_STEPS = totalSteps,
                p_CURRENT_PERIOD_YM = currentPeriodYm,
                p_CURRENT_STEP_CODE = currentStepCode,
                p_CURRENT_STEP_NAME = currentStepName,
                p_MESSAGE = message,
                p_STATUS = status
            };

            return await ExecuteAsync(proc, param, databaseName);
        }

        public async Task<int> UpsertMonthAsync(
            string companyCd,
            string periodYm,
            string fromYmd,
            string toYmd,
            string status,
            string? message,
            string userId,
            string? databaseName = null)
        {
            const string proc = @"
                CALL sp_period_lock_month_upsert(
                    @p_COMPANY_CD,
                    @p_PERIOD_YM,
                    @p_FROM_YMD,
                    @p_TO_YMD,
                    @p_STATUS,
                    @p_MESSAGE,
                    @p_USER_ID
                );";

            return await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_STATUS = status,
                p_MESSAGE = message ?? string.Empty,
                p_USER_ID = userId
            }, databaseName);
        }

        public async Task<int> UpsertStepAsync(
            string companyCd,
            string periodYm,
            string stepCode,
            string stepName,
            int stepOrder,
            string status,
            string? message,
            string userId,
            string? databaseName = null)
        {
            const string proc = @"
                CALL sp_period_lock_step_upsert(
                    @p_COMPANY_CD,
                    @p_PERIOD_YM,
                    @p_STEP_CODE,
                    @p_STEP_NAME,
                    @p_STEP_ORDER,
                    @p_STATUS,
                    @p_MESSAGE,
                    @p_USER_ID
                );";

            return await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_STEP_CODE = stepCode,
                p_STEP_NAME = stepName,
                p_STEP_ORDER = stepOrder,
                p_STATUS = status,
                p_MESSAGE = message ?? string.Empty,
                p_USER_ID = userId
            }, databaseName);
        }

        public async Task<int> UpsertTaskAsync(
            string companyCd,
            string periodYm,
            string stepCode,
            string taskCode,
            string taskName,
            int taskOrder,
            string status,
            string? message,
            string userId,
            string? databaseName = null)
        {
            const string proc = @"
                CALL sp_period_lock_task_upsert(
                    @p_COMPANY_CD,
                    @p_PERIOD_YM,
                    @p_STEP_CODE,
                    @p_TASK_CODE,
                    @p_TASK_NAME,
                    @p_TASK_ORDER,
                    @p_STATUS,
                    @p_MESSAGE,
                    @p_USER_ID
                );";

            return await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_STEP_CODE = stepCode,
                p_TASK_CODE = taskCode,
                p_TASK_NAME = taskName,
                p_TASK_ORDER = taskOrder,
                p_STATUS = status,
                p_MESSAGE = message ?? string.Empty,
                p_USER_ID = userId
            }, databaseName);
        }

        public async Task<int> AddJobLogAsync(
            string companyCd,
            string jobId,
            string periodYm,
            string stepCode,
            string taskCode,
            string taskName,
            string status,
            string? message,
            string userId)
        {
            //Không cần ghi log 
            return 0;
        }

        public async Task<string> DeriveMonthStatusAsync(string companyCd, string periodYm, string? databaseName = null)
        {
            const string query = "CALL sp_period_lock_month_derive_status(@p_COMPANY_CD, @p_PERIOD_YM)";

            var rows = await _db.QueryAsync<PeriodLockStatusResult>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_PERIOD_YM = periodYm
                },
                databaseName
            );

            return rows.FirstOrDefault()?.Status ?? PeriodLockMonthStatus.Open;
        }

        private async Task<int> UnlockPeriodStepAsync(
            string companyCd,
            string periodYm,
            string stepCode,
            string? reason,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(stepCode, PeriodLockStepCode.FaPrepaidLock, StringComparison.OrdinalIgnoreCase))
            {
                const string faPrepaidUnlockProc = @"
                    CALL sp_period_lock_fa_prepaid_unlock(
                        @p_COMPANY_CD,
                        @p_PERIOD_YM,
                        @p_REASON,
                        @p_USER_ID
                    );";

                await ExecuteAsync(faPrepaidUnlockProc, new
                {
                    p_COMPANY_CD = companyCd,
                    p_PERIOD_YM = periodYm,
                    p_REASON = reason ?? string.Empty,
                    p_USER_ID = userId
                }, databaseName);

                /*
                 * sp_period_lock_fa_prepaid_unlock chỉ rollback dữ liệu TSCĐ.
                 * Cần cập nhật metadata khóa sổ giống sp_period_lock_period_unlock.
                 */
                await UpsertStepAsync(
                    companyCd,
                    periodYm,
                    PeriodLockStepCode.FaPrepaidLock,
                    GetStepName(PeriodLockStepCode.FaPrepaidLock),
                    GetStepOrder(PeriodLockStepCode.FaPrepaidLock),
                    PeriodLockStepStatus.Open,
                    "Đã mở",
                    userId,
                    databaseName);

                var monthStatus = await DeriveMonthStatusAsync(companyCd, periodYm, databaseName);
                return await UpsertMonthAsync(
                    companyCd,
                    periodYm,
                    GetFromYmd(periodYm),
                    GetToYmd(periodYm),
                    monthStatus,
                    "Đã mở Khóa TSCĐ / CP trả trước",
                    userId,
                    databaseName);
            }

            if (string.Equals(stepCode, PeriodLockStepCode.CogsSummary, StringComparison.OrdinalIgnoreCase))
            {
                const string cogsUnlockProc = @"
                    CALL sp_period_lock_cogs_unlock(
                        @p_COMPANY_CD,
                        @p_PERIOD_YM,
                        @p_REASON,
                        @p_USER_ID
                    );";

                await ExecuteAsync(cogsUnlockProc, new
                {
                    p_COMPANY_CD = companyCd,
                    p_PERIOD_YM = periodYm,
                    p_REASON = reason ?? string.Empty,
                    p_USER_ID = userId
                }, databaseName);

                await UpsertStepAsync(
                    companyCd,
                    periodYm,
                    PeriodLockStepCode.CogsSummary,
                    GetStepName(PeriodLockStepCode.CogsSummary),
                    GetStepOrder(PeriodLockStepCode.CogsSummary),
                    PeriodLockStepStatus.Open,
                    "Đã mở",
                    userId,
                    databaseName);

                var monthStatus = await DeriveMonthStatusAsync(companyCd, periodYm, databaseName);
                return await UpsertMonthAsync(
                    companyCd,
                    periodYm,
                    GetFromYmd(periodYm),
                    GetToYmd(periodYm),
                    monthStatus,
                    "Đã mở Báo cáo về tổng hợp giá vốn",
                    userId,
                    databaseName);
            }

            if (string.Equals(stepCode, PeriodLockStepCode.ProfitLossReport, StringComparison.OrdinalIgnoreCase))
            {
                const string plUnlockProc = @"
                    CALL sp_period_lock_pl_unlock(
                        @p_COMPANY_CD,
                        @p_PERIOD_YM,
                        @p_REASON,
                        @p_USER_ID
                    );";

                await ExecuteAsync(plUnlockProc, new
                {
                    p_COMPANY_CD = companyCd,
                    p_PERIOD_YM = periodYm,
                    p_REASON = reason ?? string.Empty,
                    p_USER_ID = userId
                }, databaseName);

                await UpsertStepAsync(
                    companyCd,
                    periodYm,
                    PeriodLockStepCode.ProfitLossReport,
                    GetStepName(PeriodLockStepCode.ProfitLossReport),
                    GetStepOrder(PeriodLockStepCode.ProfitLossReport),
                    PeriodLockStepStatus.Open,
                    "Đã mở",
                    userId,
                    databaseName);

                var monthStatus = await DeriveMonthStatusAsync(companyCd, periodYm, databaseName);
                return await UpsertMonthAsync(
                    companyCd,
                    periodYm,
                    GetFromYmd(periodYm),
                    GetToYmd(periodYm),
                    monthStatus,
                    "Đã mở Báo cáo lãi lỗ",
                    userId,
                    databaseName);
            }

            const string proc = @"
                CALL sp_period_lock_period_unlock(
                    @p_COMPANY_CD,
                    @p_PERIOD_YM,
                    @p_STEP_CODE,
                    @p_REASON,
                    @p_USER_ID
                );";

            return await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_STEP_CODE = stepCode,
                p_REASON = reason ?? string.Empty,
                p_USER_ID = userId
            }, databaseName);
        }

        public async Task RunJobAsync(string companyCd, string jobId, string userId, string databaseName, CancellationToken cancellationToken)
        {
            var request = await GetJobRequestAsync(companyCd, jobId, databaseName);

            if (request == null)
            {
                await UpdateJobProgressAsync(
                    companyCd,
                    jobId,
                    0,
                    0,
                    string.Empty,
                    string.Empty,
                    "-",
                    "Không tìm thấy request của job khóa sổ",
                    PeriodLockJobStatus.Error,
                    databaseName
                );

                return;
            }

            await RunJobAsync(companyCd, jobId, request, userId, databaseName, cancellationToken);
        }

        public async Task RunJobAsync(
            string companyCd,
            string jobId,
            StartPeriodLockRequest request,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            var lockStepCodes = NormalizeLockStepCodes(request);
            request.Steps = lockStepCodes;
            request.FromPeriodYm = await ResolveLockFromPeriodYmAsync(
                companyCd,
                request,
                databaseName
            );
            PopulateLockTargetMetadata(request);

            var targetStepCode = request.Options?.TargetStepCode ?? ResolveTargetStepCode(request);
            var targetStepName = request.Options?.TargetStepName
                ?? GetStepName(targetStepCode ?? string.Empty);

            if (string.Compare(request.FromPeriodYm, request.ToPeriodYm, StringComparison.Ordinal) > 0)
            {
                await UpdateJobProgressAsync(
                    companyCd,
                    jobId,
                    1,
                    1,
                    request.ToPeriodYm,
                    targetStepCode ?? string.Empty,
                    targetStepName,
                    "Các bước đã được khóa trước đó, không có kỳ cần xử lý",
                    PeriodLockJobStatus.Done,
                    databaseName
                );

                return;
            }

            var totalSteps = await CalculateLockTotalStepsAsync(companyCd, request, databaseName);
            var doneSteps = 0;
            var currentPeriodYm = string.Empty;
            var currentStepCode = targetStepCode ?? string.Empty;
            var currentStepName = targetStepName;

            await UpdateJobProgressAsync(
                companyCd,
                jobId,
                doneSteps,
                totalSteps,
                request.FromPeriodYm,
                currentStepCode,
                currentStepName,
                "Đang chuẩn bị xử lý khóa sổ",
                PeriodLockJobStatus.Running,
                databaseName
            );

            try
            {
                foreach (var periodYm in GetPeriodRange(request.FromPeriodYm, request.ToPeriodYm))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    currentPeriodYm = periodYm;
                    var fromYmd = GetFromYmd(periodYm);
                    var toYmd = GetToYmd(periodYm);

                    await UpsertMonthAsync(companyCd, periodYm, fromYmd, toYmd, PeriodLockMonthStatus.Processing, "Đang xử lý khóa sổ", userId, databaseName);

                    if (lockStepCodes.Contains(PeriodLockStepCode.FaPrepaidLock))
                    {
                        currentStepCode = PeriodLockStepCode.FaPrepaidLock;
                        currentStepName = "Khóa TSCĐ / CP trả trước";

                        if (!await IsStepDoneAsync(companyCd, periodYm, PeriodLockStepCode.FaPrepaidLock, databaseName))
                        {
                            doneSteps = await RunFaPrepaidLockWithProgressAsync(
                                companyCd,
                                jobId,
                                periodYm,
                                totalSteps,
                                doneSteps,
                                userId,
                                databaseName,
                                cancellationToken
                            );
                        }
                    }

                    if (lockStepCodes.Contains(PeriodLockStepCode.CogsSummary))
                    {
                        await ValidateRequiredPreviousSelectedStepDoneUntilPeriodAsync(
                            companyCd,
                            periodYm,
                            PeriodLockStepCode.CogsSummary,
                            lockStepCodes,
                            databaseName
                        );

                        var transferRules = request.Options?.CogsSummary?.TransferRules ?? CogsTransferRuleCode.Transfer154To632;
                        currentStepCode = PeriodLockStepCode.CogsSummary;
                        currentStepName = "Báo cáo về tổng hợp giá vốn";

                        if (!await IsStepDoneAsync(companyCd, periodYm, PeriodLockStepCode.CogsSummary, databaseName))
                        {
                            doneSteps = await RunCogsSummaryWithProgressAsync(
                                companyCd,
                                jobId,
                                periodYm,
                                transferRules,
                                totalSteps,
                                doneSteps,
                                userId,
                                databaseName,
                                cancellationToken
                            );
                        }
                    }

                    if (lockStepCodes.Contains(PeriodLockStepCode.ProfitLossReport))
                    {
                        await ValidateRequiredPreviousSelectedStepDoneUntilPeriodAsync(
                            companyCd,
                            periodYm,
                            PeriodLockStepCode.ProfitLossReport,
                            lockStepCodes,
                            databaseName
                        );

                        var balanceMethod = request.Options?.ProfitLossReport?.BalanceMethod ?? ProfitLossBalanceMethod.BalanceAccountTwoSide;

                        currentStepCode = PeriodLockStepCode.ProfitLossReport;
                        currentStepName = GetStepName(PeriodLockStepCode.ProfitLossReport);

                        if (!await IsStepDoneAsync(companyCd, periodYm, PeriodLockStepCode.ProfitLossReport, databaseName))
                        {
                            doneSteps = await RunProfitLossWithProgressAsync(
                                companyCd,
                                jobId,
                                periodYm,
                                balanceMethod,
                                totalSteps,
                                doneSteps,
                                userId,
                                databaseName,
                                cancellationToken
                            );
                        }
                    }

                    var monthStatus = await DeriveMonthStatusAsync(companyCd, periodYm, databaseName);

                    await UpsertMonthAsync(
                        companyCd,
                        periodYm,
                        fromYmd,
                        toYmd,
                        monthStatus,
                        monthStatus == PeriodLockMonthStatus.Locked ? "Khóa sổ hoàn tất" : "Đã xử lý một phần",
                        userId,
                        databaseName
                    );
                }

                await UpdateJobProgressAsync(
                    companyCd,
                    jobId,
                    totalSteps,
                    totalSteps,
                    request.ToPeriodYm,
                    targetStepCode ?? string.Empty,
                    targetStepName,
                    "Xử lý hoàn tất",
                    PeriodLockJobStatus.Done,
                    databaseName
                );
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(currentPeriodYm))
                {
                    await UpsertMonthAsync(companyCd, currentPeriodYm, GetFromYmd(currentPeriodYm), GetToYmd(currentPeriodYm), PeriodLockMonthStatus.Error, ex.Message, userId, databaseName);
                }

                await UpdateJobProgressAsync(
                    companyCd,
                    jobId,
                    doneSteps,
                    totalSteps,
                    currentPeriodYm,
                    currentStepCode,
                    currentStepName,
                    ex.Message,
                    PeriodLockJobStatus.Error,
                    databaseName
                );
                throw;
            }
        }

        private async Task<int> RunProgressUnitAsync(
            string companyCd,
            string jobId,
            string periodYm,
            string stepCode,
            string stepName,
            string progressName,
            int totalSteps,
            int doneSteps,
            string userId,
            string databaseName,
            CancellationToken cancellationToken,
            Func<Task> action)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await UpdateJobProgressAsync(
                companyCd,
                jobId,
                doneSteps,
                totalSteps,
                periodYm,
                stepCode,
                stepName,
                $"Đang {progressName} kỳ {FormatPeriodLabel(periodYm)}",
                PeriodLockJobStatus.Running,
                databaseName
            );

            await action();

            doneSteps++;

            await UpdateJobProgressAsync(
                companyCd,
                jobId,
                doneSteps,
                totalSteps,
                periodYm,
                stepCode,
                stepName,
                $"Hoàn tất {progressName} kỳ {FormatPeriodLabel(periodYm)}",
                PeriodLockJobStatus.Running,
                databaseName
            );

            return doneSteps;
        }

        public async Task RunUnlockJobAsync(
            string companyCd,
            string jobId,
            StartPeriodUnlockRequest request,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            var unlockStepCodes = NormalizeUnlockStepCodes(request);

            /*
             * Không tin ToPeriodYm từ frontend, vì màn hình có thể bị cũ nếu máy khác vừa khóa thêm kỳ.
             * Backend luôn lấy tháng lớn nhất còn step DONE trong DB làm kỳ kết thúc mở sổ thực tế.
             * Ví dụ frontend đang thấy khóa tới 08/2026 nhưng DB đã khóa tới 09/2026, backend sẽ chạy tới 09/2026.
             */
            var effectiveToPeriodYm = await GetLatestUnlockablePeriodYmAsync(companyCd, databaseName);

            if (string.IsNullOrWhiteSpace(effectiveToPeriodYm))
            {
                await UpdateJobProgressAsync(
                    companyCd,
                    jobId,
                    1,
                    1,
                    request.FromPeriodYm,
                    string.Empty,
                    "-",
                    "Không có kỳ nào cần mở sổ",
                    PeriodLockJobStatus.Done,
                    databaseName
                );

                return;
            }

            if (string.Compare(request.FromPeriodYm, effectiveToPeriodYm, StringComparison.Ordinal) > 0)
            {
                await UpdateJobProgressAsync(
                    companyCd,
                    jobId,
                    1,
                    1,
                    request.FromPeriodYm,
                    string.Empty,
                    "-",
                    $"Không có kỳ cần mở từ {FormatPeriodLabel(request.FromPeriodYm)} đến {FormatPeriodLabel(effectiveToPeriodYm)}",
                    PeriodLockJobStatus.Done,
                    databaseName
                );

                return;
            }

            request.ToPeriodYm = effectiveToPeriodYm;

            var totalSteps = GetMonthCountInclusive(request.FromPeriodYm, effectiveToPeriodYm) * Math.Max(unlockStepCodes.Count, 1);
            var doneSteps = 0;
            var currentPeriodYm = string.Empty;
            var currentStepCode = string.Empty;
            var currentStepName = "Mở sổ kỳ kế toán";

            try
            {
                foreach (var periodYm in GetPeriodRangeDescending(request.FromPeriodYm, effectiveToPeriodYm))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    currentPeriodYm = periodYm;

                    foreach (var stepCode in unlockStepCodes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        currentStepCode = stepCode;
                        currentStepName = GetStepName(stepCode);

                        await UpdateJobProgressAsync(
                            companyCd,
                            jobId,
                            doneSteps,
                            totalSteps,
                            periodYm,
                            stepCode,
                            currentStepName,
                            $"Đang mở {currentStepName} kỳ {FormatPeriodLabel(periodYm)}",
                            PeriodLockJobStatus.Running,
                            databaseName
                        );

                        await UnlockPeriodStepAsync(
                            companyCd,
                            periodYm,
                            stepCode,
                            request.Reason,
                            userId,
                            databaseName,
                            cancellationToken
                        );

                        doneSteps++;

                        await UpdateJobProgressAsync(
                            companyCd,
                            jobId,
                            doneSteps,
                            totalSteps,
                            periodYm,
                            stepCode,
                            currentStepName,
                            $"Đã mở {currentStepName} kỳ {FormatPeriodLabel(periodYm)}",
                            PeriodLockJobStatus.Running,
                            databaseName
                        );
                    }
                }

                await UpdateJobProgressAsync(companyCd, jobId, totalSteps, totalSteps, request.ToPeriodYm, string.Empty, "-", "Mở sổ hoàn tất", PeriodLockJobStatus.Done, databaseName);
            }
            catch (Exception ex)
            {
                await UpdateJobProgressAsync(companyCd, jobId, doneSteps, totalSteps, currentPeriodYm, currentStepCode, currentStepName, ex.Message, PeriodLockJobStatus.Error, databaseName);
                throw;
            }
        }

        private async Task<int> RunSingleTaskAsync(
            string companyCd,
            string jobId,
            string periodYm,
            string stepCode,
            string stepName,
            int stepOrder,
            string taskCode,
            string taskName,
            int taskOrder,
            int totalSteps,
            int doneSteps,
            string userId,
            bool completeStepAfterTask,
            string databaseName,
            CancellationToken cancellationToken,
            Func<Task> action)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await UpsertStepAsync(companyCd, periodYm, stepCode, stepName, stepOrder, PeriodLockStepStatus.Processing, $"Đang xử lý {taskName}", userId, databaseName);
                await UpsertTaskAsync(companyCd, periodYm, stepCode, taskCode, taskName, taskOrder, PeriodLockStepStatus.Processing, $"Đang xử lý {taskName}", userId, databaseName);

                await UpdateJobProgressAsync(companyCd, jobId, doneSteps, totalSteps, periodYm, stepCode, taskName, $"Đang xử lý {taskName} kỳ {FormatPeriodLabel(periodYm)}", PeriodLockJobStatus.Running, databaseName);

                await action();
                doneSteps++;

                await UpsertTaskAsync(companyCd, periodYm, stepCode, taskCode, taskName, taskOrder, PeriodLockStepStatus.Done, "Hoàn tất", userId, databaseName);

                if (completeStepAfterTask)
                {
                    await UpsertStepAsync(companyCd, periodYm, stepCode, stepName, stepOrder, PeriodLockStepStatus.Done, "Hoàn tất", userId, databaseName);
                }

                await UpdateJobProgressAsync(companyCd, jobId, doneSteps, totalSteps, periodYm, stepCode, taskName, $"Hoàn tất {taskName} kỳ {FormatPeriodLabel(periodYm)}", PeriodLockJobStatus.Running, databaseName);

                return doneSteps;
            }
            catch (Exception ex)
            {
                await UpsertTaskAsync(companyCd, periodYm, stepCode, taskCode, taskName, taskOrder, PeriodLockStepStatus.Error, ex.Message, userId, databaseName);
                await UpsertStepAsync(companyCd, periodYm, stepCode, stepName, stepOrder, PeriodLockStepStatus.Error, ex.Message, userId, databaseName);
                throw;
            }
        }

        private async Task<int> RunCogsSummaryWithProgressAsync(
            string companyCd,
            string jobId,
            string periodYm,
            string ruleCode,
            int totalSteps,
            int doneSteps,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            const string stepCode = PeriodLockStepCode.CogsSummary;
            const string stepName = "Báo cáo về tổng hợp giá vốn";
            const int stepOrder = 2;

            await UpsertStepAsync(
                companyCd,
                periodYm,
                stepCode,
                stepName,
                stepOrder,
                PeriodLockStepStatus.Processing,
                "Đang tổng hợp giá vốn",
                userId,
                databaseName
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "kiểm tra dữ liệu giá vốn",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunCogsValidateAsync(companyCd, periodYm, ruleCode, userId, databaseName, cancellationToken)
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "tính kết chuyển giá vốn",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunCogsCalculateAsync(companyCd, periodYm, ruleCode, userId, databaseName, cancellationToken)
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "tạo chứng từ kết chuyển giá vốn",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunCogsCreateVoucherAsync(companyCd, periodYm, ruleCode, userId, databaseName, cancellationToken)
            );

            await UpsertStepAsync(
                companyCd,
                periodYm,
                stepCode,
                stepName,
                stepOrder,
                PeriodLockStepStatus.Done,
                "Hoàn tất tổng hợp giá vốn",
                userId,
                databaseName
            );

            return doneSteps;
        }

        private async Task<int> RunProfitLossWithProgressAsync(
            string companyCd,
            string jobId,
            string periodYm,
            string balanceMethod,
            int totalSteps,
            int doneSteps,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            const string stepCode = PeriodLockStepCode.ProfitLossReport;
            const string stepName = "Báo cáo lãi lỗ";
            const int stepOrder = 3;

            await UpsertStepAsync(
                companyCd,
                periodYm,
                stepCode,
                stepName,
                stepOrder,
                PeriodLockStepStatus.Processing,
                "Đang xử lý báo cáo lãi lỗ",
                userId,
                databaseName
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "kiểm tra dữ liệu báo cáo lãi lỗ",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunProfitLossValidateAsync(companyCd, periodYm, balanceMethod, userId, databaseName, cancellationToken)
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "tính kết chuyển lãi lỗ",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunProfitLossCalculateAsync(companyCd, periodYm, balanceMethod, userId, databaseName, cancellationToken)
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "tạo chứng từ và snapshot số dư cuối kỳ",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunProfitLossCreateVoucherAsync(companyCd, periodYm, balanceMethod, userId, databaseName, cancellationToken)
            );

            await UpsertStepAsync(
                companyCd,
                periodYm,
                stepCode,
                stepName,
                stepOrder,
                PeriodLockStepStatus.Done,
                "Hoàn tất báo cáo lãi lỗ",
                userId,
                databaseName
            );

            return doneSteps;
        }

        private async Task<int> ExecuteAsync(string proc, object param, string? databaseName = null)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);

            try
            {
                var result = await session.ExecuteAsync(proc, param);
                session.Commit();
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task<int> RunFaPrepaidLockWithProgressAsync(
            string companyCd,
            string jobId,
            string periodYm,
            int totalSteps,
            int doneSteps,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            const string stepCode = PeriodLockStepCode.FaPrepaidLock;
            const string stepName = "Khóa TSCĐ / CP trả trước";
            const int stepOrder = 1;

            await UpsertStepAsync(
                companyCd,
                periodYm,
                stepCode,
                stepName,
                stepOrder,
                PeriodLockStepStatus.Processing,
                "Đang xử lý khóa TSCĐ / CP trả trước",
                userId,
                databaseName
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "kiểm tra dữ liệu TSCĐ / CP trả trước",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunFaPrepaidValidateAsync(companyCd, periodYm, userId, databaseName, cancellationToken)
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "tính khấu hao / phân bổ",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunFaPrepaidCalculateAsync(companyCd, periodYm, userId, databaseName, cancellationToken)
            );

            doneSteps = await RunProgressUnitAsync(
                companyCd,
                jobId,
                periodYm,
                stepCode,
                stepName,
                "tạo chứng từ khóa TSCĐ / CP trả trước",
                totalSteps,
                doneSteps,
                userId,
                databaseName,
                cancellationToken,
                () => RunFaPrepaidCreateVoucherAsync(companyCd, periodYm, userId, databaseName, cancellationToken)
            );

            await UpsertStepAsync(
                companyCd,
                periodYm,
                stepCode,
                stepName,
                stepOrder,
                PeriodLockStepStatus.Done,
                "Hoàn tất khóa TSCĐ / CP trả trước",
                userId,
                databaseName
            );

            return doneSteps;
        }

        private async Task RunFaPrepaidValidateAsync(
            string companyCd,
            string periodYm,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_fa_prepaid_validate(@p_COMPANY_CD, @p_PERIOD_YM, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunFaPrepaidCalculateAsync(
            string companyCd,
            string periodYm,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_fa_prepaid_calculate(@p_COMPANY_CD, @p_PERIOD_YM, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunFaPrepaidCreateVoucherAsync(
            string companyCd,
            string periodYm,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_fa_prepaid_create_voucher(@p_COMPANY_CD, @p_PERIOD_YM, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunCogsValidateAsync(
            string companyCd,
            string periodYm,
            string ruleCode,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_cogs_validate(@p_COMPANY_CD, @p_PERIOD_YM, @p_RULE_CODE, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_RULE_CODE = ruleCode,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunCogsCalculateAsync(
            string companyCd,
            string periodYm,
            string ruleCode,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_cogs_calculate(@p_COMPANY_CD, @p_PERIOD_YM, @p_RULE_CODE, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_RULE_CODE = ruleCode,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunCogsCreateVoucherAsync(
            string companyCd,
            string periodYm,
            string ruleCode,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_cogs_create_voucher(@p_COMPANY_CD, @p_PERIOD_YM, @p_RULE_CODE, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_RULE_CODE = ruleCode,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunProfitLossValidateAsync(
            string companyCd,
            string periodYm,
            string balanceMethod,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_pl_validate(@p_COMPANY_CD, @p_PERIOD_YM, @p_BALANCE_METHOD, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_BALANCE_METHOD = balanceMethod,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunProfitLossCalculateAsync(
            string companyCd,
            string periodYm,
            string balanceMethod,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_pl_calculate(@p_COMPANY_CD, @p_PERIOD_YM, @p_BALANCE_METHOD, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_BALANCE_METHOD = balanceMethod,
                p_USER_ID = userId
            }, databaseName);
        }

        private async Task RunProfitLossCreateVoucherAsync(
            string companyCd,
            string periodYm,
            string balanceMethod,
            string userId,
            string databaseName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string proc = "CALL sp_period_lock_pl_create_voucher(@p_COMPANY_CD, @p_PERIOD_YM, @p_BALANCE_METHOD, @p_USER_ID)";

            await ExecuteAsync(proc, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm,
                p_BALANCE_METHOD = balanceMethod,
                p_USER_ID = userId
            }, databaseName);
        }

        private sealed class PeriodStepStatusRow
        {
            public string? Status { get; set; }
        }

        private async Task<string> GetPeriodStepStatusAsync(string companyCd, string periodYm, string stepCode, string? databaseName = null)
        {
            const string query = "CALL sp_period_lock_step_status_get(@p_COMPANY_CD, @p_PERIOD_YM, @p_STEP_CODE)";

            var rows = await _db.QueryAsync<PeriodStepStatusRow>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_PERIOD_YM = periodYm,
                    p_STEP_CODE = stepCode
                },
                databaseName
            );

            return rows.FirstOrDefault()?.Status ?? PeriodLockStepStatus.Open;
        }

        private async Task<bool> IsStepDoneAsync(string companyCd, string periodYm, string stepCode, string? databaseName = null)
        {
            return string.Equals(
                await GetPeriodStepStatusAsync(companyCd, periodYm, stepCode, databaseName),
                PeriodLockStepStatus.Done,
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static string? ResolveTargetStepCode(StartPeriodLockRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TargetStepCode))
            {
                return request.TargetStepCode.Trim();
            }

            if (request.TargetStepCodes != null && request.TargetStepCodes.Count > 0)
            {
                return NormalizeStepCodeList(request.TargetStepCodes)
                    .OrderByDescending(GetStepOrder)
                    .FirstOrDefault();
            }

            var lockStepCodes = NormalizeLockStepCodes(request);
            return lockStepCodes
                .OrderByDescending(GetStepOrder)
                .FirstOrDefault();
        }

        private async Task ValidateRequiredStepDoneUntilPeriodAsync(
            string companyCd,
            string targetPeriodYm,
            string requiredStepCode,
            string targetStepCode,
            string? databaseName = null)
        {
            var fiscalStartYmd = await GetFiscalStartYmdAsync(companyCd, databaseName);
            var startPeriodYm = GetFiscalStartPeriodYm(fiscalStartYmd);

            foreach (var periodYm in GetPeriodRange(startPeriodYm, targetPeriodYm))
            {
                var status = await GetPeriodStepStatusAsync(companyCd, periodYm, requiredStepCode, databaseName);

                if (!string.Equals(status, PeriodLockStepStatus.Done, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Kỳ {FormatPeriodLabel(periodYm)} chưa khóa xong {GetStepName(requiredStepCode)}, không thể khóa {GetStepName(targetStepCode)}."
                    );
                }
            }
        }

        private async Task ValidateRequiredPreviousSelectedStepDoneUntilPeriodAsync(
            string companyCd,
            string targetPeriodYm,
            string targetStepCode,
            List<string> selectedStepCodes,
            string? databaseName = null)
        {
            var requiredPreviousStepCode = GetRequiredPreviousSelectedStepCode(targetStepCode, selectedStepCodes);

            if (string.IsNullOrWhiteSpace(requiredPreviousStepCode))
            {
                return;
            }

            await ValidateRequiredStepDoneUntilPeriodAsync(
                companyCd,
                targetPeriodYm,
                requiredPreviousStepCode,
                targetStepCode,
                databaseName
            );
        }

        private static string? GetRequiredPreviousSelectedStepCode(
            string targetStepCode,
            List<string> selectedStepCodes)
        {
            var targetOrder = GetStepOrder(targetStepCode);

            if (targetOrder <= 1)
            {
                return null;
            }

            return selectedStepCodes
                .Where(stepCode => GetStepOrder(stepCode) > 0 && GetStepOrder(stepCode) < targetOrder)
                .OrderByDescending(GetStepOrder)
                .FirstOrDefault();
        }

        private async Task<string> GetLatestUnlockablePeriodYmAsync(string companyCd, string? databaseName = null)
        {
            /*
             * Mở sổ phải chạy từ tháng lớn nhất còn bất kỳ step DONE nào.
             * Nguồn xác định lấy từ sp_period_lock_current_status_get.AnyStepLockedPeriodYm.
             */
            var currentStatus = await GetCurrentStatusAsync(companyCd, databaseName);
            return currentStatus?.AnyStepLockedPeriodYm ?? string.Empty;
        }

        private static bool IsPopupLockStepRequest(StartPeriodLockRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.TargetStepCode)
                || (request.TargetStepCodes != null && request.TargetStepCodes.Count > 0);
        }

        private static List<string> NormalizeLockStepCodes(StartPeriodLockRequest request)
        {
            /*
             * Quy ước khóa từng bước có xét checkbox chính:
             * - request.Steps là danh sách checkbox chính đang được chọn.
             * - Step nào không nằm trong request.Steps thì bỏ qua hoàn toàn, không chạy và không validate dependency.
             * - Flow cũ: dùng request.Steps đúng như user chọn.
             * - Flow popup: TargetStepCode/TargetStepCodes chỉ là "khóa tới bước nào".
             *   Backend expand 1 -> 2 -> 3 rồi lọc lại theo request.Steps.
             *
             * Ví dụ:
             * - TargetStepCode = PROFIT_LOSS_REPORT
             * - request.Steps = [COGS_SUMMARY, PROFIT_LOSS_REPORT]
             * => chạy [COGS_SUMMARY, PROFIT_LOSS_REPORT], bỏ qua FA_PREPAID_LOCK.
             */
            var selectedStepCodes = NormalizeStepCodeList(request.Steps);

            if (!string.IsNullOrWhiteSpace(request.TargetStepCode) ||
                (request.TargetStepCodes != null && request.TargetStepCodes.Count > 0))
            {
                var rawStepCodes = request.TargetStepCodes != null && request.TargetStepCodes.Count > 0
                    ? request.TargetStepCodes
                    : new List<string> { request.TargetStepCode ?? string.Empty };

                var normalizedStepCodes = NormalizeStepCodeList(rawStepCodes);

                if (normalizedStepCodes.Count == 0)
                {
                    throw new InvalidOperationException("Bước khóa sổ không hợp lệ.");
                }

                var targetStepCode = normalizedStepCodes
                    .OrderByDescending(GetStepOrder)
                    .FirstOrDefault() ?? PeriodLockStepCode.ProfitLossReport;

                var expandedStepCodes = ExpandLockStepCodesToTarget(targetStepCode);

                var effectiveStepCodes = expandedStepCodes
                    .Where(stepCode => selectedStepCodes.Contains(stepCode, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(GetStepOrder)
                    .ToList();

                if (effectiveStepCodes.Count == 0)
                {
                    throw new InvalidOperationException("Vui lòng chọn ít nhất một bước khóa sổ đang áp dụng.");
                }

                return effectiveStepCodes;
            }

            return selectedStepCodes
                .OrderBy(GetStepOrder)
                .ToList();
        }

        private static List<string> NormalizeStepCodeList(IEnumerable<string>? stepCodes)
        {
            var normalizedStepCodes = (stepCodes ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var invalidStepCode = normalizedStepCodes.FirstOrDefault(x => GetStepOrder(x) <= 0);
            if (!string.IsNullOrWhiteSpace(invalidStepCode))
            {
                throw new InvalidOperationException($"Bước khóa sổ không hợp lệ: {invalidStepCode}");
            }

            return normalizedStepCodes;
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

        private static List<string> NormalizeUnlockStepCodes(StartPeriodUnlockRequest request)
        {
            /*
             * Quy ước mới:
             * User chọn mở tới bước nào thì backend tự mở các bước phía sau trước.
             * - Mở tới bước 3 => mở bước 3.
             * - Mở tới bước 2 => tự mở bước 3 rồi bước 2.
             * - Mở tới bước 1 => tự mở bước 3, bước 2 rồi bước 1.
             *
             * Ưu tiên TargetStepCode (bước user chọn trong popup).
             * TargetStepCodes[] chỉ dùng khi request cũ không gửi TargetStepCode.
             */
            string targetStepCode;

            if (!string.IsNullOrWhiteSpace(request.TargetStepCode))
            {
                targetStepCode = request.TargetStepCode.Trim();
            }
            else
            {
                var rawStepCodes = request.TargetStepCodes != null && request.TargetStepCodes.Count > 0
                    ? request.TargetStepCodes
                    : new List<string> { PeriodLockStepCode.FaPrepaidLock };

                var normalizedStepCodes = rawStepCodes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                targetStepCode = normalizedStepCodes
                    .OrderBy(GetStepOrder)
                    .FirstOrDefault() ?? PeriodLockStepCode.FaPrepaidLock;
            }

            if (GetStepOrder(targetStepCode) <= 0)
            {
                throw new InvalidOperationException($"Bước mở sổ không hợp lệ: {targetStepCode}");
            }

            return ExpandUnlockStepCodesToTarget(targetStepCode);
        }

        private static List<string> ExpandUnlockStepCodesToTarget(string targetStepCode)
        {
            return targetStepCode switch
            {
                PeriodLockStepCode.ProfitLossReport => new List<string>
                {
                    PeriodLockStepCode.ProfitLossReport
                },

                PeriodLockStepCode.CogsSummary => new List<string>
                {
                    PeriodLockStepCode.ProfitLossReport,
                    PeriodLockStepCode.CogsSummary
                },

                PeriodLockStepCode.FaPrepaidLock => new List<string>
                {
                    PeriodLockStepCode.ProfitLossReport,
                    PeriodLockStepCode.CogsSummary,
                    PeriodLockStepCode.FaPrepaidLock
                },

                _ => throw new InvalidOperationException($"Bước mở sổ không hợp lệ: {targetStepCode}")
            };
        }

        private static List<string> GetDefaultUnlockStepCodes()
        {
            return new List<string>
            {
                PeriodLockStepCode.ProfitLossReport,
                PeriodLockStepCode.CogsSummary,
                PeriodLockStepCode.FaPrepaidLock
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

        private static string GetStepName(string stepCode)
        {
            return stepCode switch
            {
                PeriodLockStepCode.FaPrepaidLock => "Khóa TSCĐ / CP trả trước",
                PeriodLockStepCode.CogsSummary => "Báo cáo về tổng hợp giá vốn",
                PeriodLockStepCode.ProfitLossReport => "Báo cáo lãi lỗ",
                _ => stepCode
            };
        }

        private static int GetTaskCountPerMonth(StartPeriodLockRequest request)
        {
            return Math.Max(NormalizeLockStepCodes(request).Count, 1);
        }

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

        private static string AddMonthsToPeriodYm(string periodYm, int monthCount)
        {
            var current = new DateTime(int.Parse(periodYm.Substring(0, 4)), int.Parse(periodYm.Substring(4, 2)), 1);
            return current.AddMonths(monthCount).ToString("yyyyMM");
        }

        private static IEnumerable<string> GetPeriodRange(string fromPeriodYm, string toPeriodYm)
        {
            var current = new DateTime(int.Parse(fromPeriodYm.Substring(0, 4)), int.Parse(fromPeriodYm.Substring(4, 2)), 1);
            var end = new DateTime(int.Parse(toPeriodYm.Substring(0, 4)), int.Parse(toPeriodYm.Substring(4, 2)), 1);

            while (current <= end)
            {
                yield return current.ToString("yyyyMM");
                current = current.AddMonths(1);
            }
        }

        private static IEnumerable<string> GetPeriodRangeDescending(string fromPeriodYm, string toPeriodYm)
        {
            var start = new DateTime(int.Parse(fromPeriodYm.Substring(0, 4)), int.Parse(fromPeriodYm.Substring(4, 2)), 1);
            var current = new DateTime(int.Parse(toPeriodYm.Substring(0, 4)), int.Parse(toPeriodYm.Substring(4, 2)), 1);

            if (current < start)
            {
                throw new InvalidOperationException("ToPeriodYm phải lớn hơn hoặc bằng FromPeriodYm");
            }

            while (current >= start)
            {
                yield return current.ToString("yyyyMM");
                current = current.AddMonths(-1);
            }
        }

        private static string GetFromYmd(string periodYm) => $"{periodYm}01";

        private static string GetToYmd(string periodYm)
        {
            var year = int.Parse(periodYm.Substring(0, 4));
            var month = int.Parse(periodYm.Substring(4, 2));
            var lastDay = DateTime.DaysInMonth(year, month);
            return $"{periodYm}{lastDay:00}";
        }

        private static string FormatPeriodLabel(string periodYm)
        {
            return periodYm.Length == 6 ? $"{periodYm.Substring(4, 2)}/{periodYm.Substring(0, 4)}" : periodYm;
        }

        private static string GetCogsTransferRuleName(string ruleCode)
        {
            return ruleCode switch
            {
                CogsTransferRuleCode.Transfer154To155 => "154 >> 155 Tự động chuyển dữ liệu",
                CogsTransferRuleCode.Transfer154To155To632 => "154 >> 155 >> 632 Tự động chuyển dữ liệu",
                CogsTransferRuleCode.Transfer154To632 => "154 >> 632 Tự động chuyển dữ liệu",
                _ => ruleCode
            };
        }

        private static string GetProfitLossMethodName(string method)
        {
            return method switch
            {
                ProfitLossBalanceMethod.BalanceAccount => "Bảng cân đối tài khoản",
                ProfitLossBalanceMethod.BalanceAccountTwoSide => "Bảng cân đối tài khoản mẫu số dư 02 bên",
                _ => method
            };
        }
    }
}
