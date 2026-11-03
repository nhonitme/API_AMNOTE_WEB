using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Lập kế hoạch Confirm B2 (622/627) — pure, không DB.
/// Không tạo N154; không CONFIRMED toàn kỳ; không đổi AllocationEngine.
/// </summary>
public static class AllocationConfirmPlanner
{
    public const string StepCode = ProductionCostStepCode.CostAllocation;
    public const int StepIndex = 2;

    public sealed class Plan
    {
        public string CompanyCd { get; init; } = string.Empty;
        public string PeriodYm { get; init; } = string.Empty;
        public long RunId { get; init; }
        public int RunVersion { get; init; }
        public string OperationId { get; init; } = string.Empty;
        public string HistoryStableKey { get; init; } = string.Empty;
        public string VoucherStableKey { get; init; } = string.Empty;
        public string ContentFingerprint { get; init; } = string.Empty;
        public string? PriorRunStatus { get; init; }
        public string ExpectedRunStatusAfter { get; init; } = GkRunStatus.AllocConfirmed;
        public bool IsNoop { get; init; }
        public bool WouldPersistDetails { get; init; }
        public bool WouldCallAllocConfirmSp { get; init; }
        public bool WouldWriteHistory { get; init; }
        public bool WouldPostAccountingVoucher { get; init; } // luôn false ở B2
        public string? IdempotencyAction { get; init; }
        public string Message { get; init; } = string.Empty;
        public HistoryEntryPlan? History { get; init; }
        public AllocationComputeResult Compute { get; init; } = new();
        public int AllocateType { get; init; }
        public bool ByDepartment { get; init; }
        public int NumberDigit { get; init; }
        public bool Include6212 { get; init; }
    }

    public static string VoucherStableKey(string companyCd, string periodYm)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || string.IsNullOrWhiteSpace(periodYm))
            throw new ArgumentException("COMPANY_CD / PERIOD_YM bắt buộc cho voucher STABLE_KEY.");
        return $"ALLOC:{companyCd.Trim()}:{periodYm.Trim()}";
    }

    /// <summary>
    /// Fingerprint canonical / deterministic — InvariantCulture; đủ nội dung nghiệp vụ bảo vệ.
    /// </summary>
    public static string BuildContentFingerprint(
        AllocationComputeResult compute,
        string periodYm,
        int allocateType,
        bool byDepartment,
        int numberDigit,
        bool include6212,
        int runVersion)
    {
        ArgumentNullException.ThrowIfNull(compute);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("B2|p").Append(periodYm.Trim())
            .Append("|v").Append(runVersion.ToString(inv))
            .Append("|t").Append(allocateType.ToString(inv))
            .Append("|d").Append(byDepartment ? "1" : "0")
            .Append("|n").Append(numberDigit.ToString(inv))
            .Append("|i6212=").Append(include6212 ? "1" : "0")
            .Append("|622=").Append(compute.Total622.ToString("0.00", inv))
            .Append("|627=").Append(compute.Total627.ToString("0.00", inv))
            .Append("|pool=").Append(compute.Pool.Count.ToString(inv))
            .Append("|lines=").Append(compute.Details.Count.ToString(inv));

        foreach (var pool in compute.Pool
                     .OrderBy(x => x.AccountCd, StringComparer.Ordinal)
                     .ThenBy(x => x.DepartmentId))
        {
            sb.Append("|P:")
                .Append(pool.AccountCd?.Trim() ?? string.Empty)
                .Append(':').Append(pool.DepartmentId.ToString(inv))
                .Append('=')
                .Append(pool.AllocationCosts.ToString("0.00", inv))
                .Append('/')
                .Append(pool.Reduction.ToString("0.00", inv))
                .Append('/')
                .Append(pool.TotalAmount.ToString("0.00", inv));
        }

        foreach (var line in compute.Details
                     .OrderBy(x => x.ProductId)
                     .ThenBy(x => x.DepartmentId)
                     .ThenBy(x => x.AccountCd, StringComparer.Ordinal)
                     .ThenBy(x => x.PoolDepartmentId))
        {
            sb.Append("|L:")
                .Append(line.ProductId.ToString(inv))
                .Append(':').Append(line.DepartmentId.ToString(inv))
                .Append(':').Append(line.PoolDepartmentId.ToString(inv))
                .Append(':').Append(line.AccountCd?.Trim() ?? string.Empty)
                .Append('=')
                .Append(line.AllocatedAmount.ToString("0.00", inv))
                .Append(line.IsLastRow ? ":1" : ":0");
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))
            .ToLowerInvariant();
        return hash; // 64 hex
    }

    /// <summary>Tương thích gọi cũ — NumberDigit=0, Include6212=false, PeriodYm bắt buộc qua overload mới.</summary>
    public static string BuildContentFingerprint(
        AllocationComputeResult compute,
        int allocateType,
        bool byDepartment,
        int runVersion,
        string periodYm = "000000",
        int numberDigit = 0,
        bool include6212 = false) =>
        BuildContentFingerprint(
            compute, periodYm, allocateType, byDepartment, numberDigit, include6212, runVersion);

    public static Plan Build(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string? runStatus,
        IReadOnlyDictionary<string, string> stepStatuses,
        string operationId,
        AllocationComputeResult compute,
        int allocateType,
        bool byDepartment,
        PersistedHistoryRef? persistedHistoryFromDb,
        string? userId = null,
        string? clientFingerprint = null,
        int numberDigit = 0,
        bool include6212 = false)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        ArgumentNullException.ThrowIfNull(compute);
        if (compute.Details.Count == 0)
            throw new InvalidOperationException("Không có dòng phân bổ để Confirm B2.");

        AllocDetailKey.AssertUnique(compute.Details.Select(d =>
            AllocDetailKey.Of(d.ProductId, d.DepartmentId, d.AccountCd, d.PoolDepartmentId)));

        var op = HistoryPlan.RequireOperationId(operationId);
        var fp = string.IsNullOrWhiteSpace(clientFingerprint)
            ? BuildContentFingerprint(
                compute, periodYm.Trim(), allocateType, byDepartment, numberDigit, include6212, runVersion)
            : HistoryPlan.NormalizeContentFingerprint(clientFingerprint, required: true)!;

        stepStatuses.TryGetValue(StepCode, out var stepStatus);
        var voucherKey = VoucherStableKey(companyCd, periodYm);
        var historyKey = HistoryPlan.StableKey(
            companyCd, runId, runVersion, StepCode, HistoryActionCode.Confirm, op);

        var persisted = persistedHistoryFromDb == null
            ? Array.Empty<PersistedHistoryRef>()
            : new[] { persistedHistoryFromDb };

        // Confirm B2 luôn yêu cầu khớp fingerprint khi OpId đã có.
        var historyPlan = HistoryPlan.PlanWrite(
            historyKey, op, persisted, fp, requireContentFingerprint: true);

        if (historyPlan.Action == HistoryPlan.WriteAction.NoopAlreadyWritten)
        {
            return new Plan
            {
                CompanyCd = companyCd.Trim(),
                PeriodYm = periodYm.Trim(),
                RunId = runId,
                RunVersion = runVersion,
                OperationId = op,
                HistoryStableKey = historyKey,
                VoucherStableKey = voucherKey,
                ContentFingerprint = fp,
                PriorRunStatus = runStatus,
                ExpectedRunStatusAfter = GkRunStatus.AfterAllocationConfirm(runStatus),
                IsNoop = true,
                WouldPersistDetails = false,
                WouldCallAllocConfirmSp = false,
                WouldWriteHistory = false,
                WouldPostAccountingVoucher = false,
                IdempotencyAction = historyPlan.Action.ToString(),
                Message = "Confirm B2 idempotent — cùng OperationId + fingerprint; không ghi lại.",
                History = null,
                Compute = compute,
                AllocateType = allocateType,
                ByDepartment = byDepartment,
                NumberDigit = numberDigit,
                Include6212 = include6212
            };
        }

        // Khóa phân bổ: không overwrite pool/detail (trừ khi đã Noop ở trên).
        AllocUpsertRules.EnsureCanPersist(
            companyCd, runId, runVersion, runVersion, runStatus, stepStatus);

        // AfterAllocationConfirm: DRAFT/… → ALLOC_CONFIRMED; giữ CONFIRMED nếu đã là toàn kỳ (không hạ).
        var expectedStatus = GkRunStatus.AfterAllocationConfirm(runStatus);
        if (GkRunStatus.IsFullPeriodConfirmed(expectedStatus)
            && !GkRunStatus.IsFullPeriodConfirmed(runStatus))
            throw new InvalidOperationException("B2 không được chuyển RUN thành CONFIRMED toàn kỳ.");

        var historyEntry = new HistoryEntryPlan
        {
            StableKey = historyKey,
            OperationId = op,
            ActionCode = HistoryActionCode.Confirm,
            StepCode = StepCode,
            StepIndex = StepIndex,
            RunId = runId,
            RunVersionBefore = runVersion,
            RunVersionAfter = runVersion,
            StatusBefore = runStatus,
            StatusAfter = expectedStatus,
            Reason = $"ALLOC_CONFIRMED; 622={compute.Total622:0.00}; 627={compute.Total627:0.00}",
            UserId = userId,
            ActionAtUtc = DateTime.UtcNow,
            IdempotencyAction = historyPlan.Action.ToString(),
            WritesHistory = true,
            ClientHistoryHintIsNotAuthoritative = true
        };

        return new Plan
        {
            CompanyCd = companyCd.Trim(),
            PeriodYm = periodYm.Trim(),
            RunId = runId,
            RunVersion = runVersion,
            OperationId = op,
            HistoryStableKey = historyKey,
            VoucherStableKey = voucherKey,
            ContentFingerprint = fp,
            PriorRunStatus = runStatus,
            ExpectedRunStatusAfter = expectedStatus,
            IsNoop = false,
            WouldPersistDetails = true,
            WouldCallAllocConfirmSp = true,
            WouldWriteHistory = true,
            WouldPostAccountingVoucher = false,
            IdempotencyAction = historyPlan.Action.ToString(),
            Message = "Plan Confirm B2 — persist detail + sp_pc_alloc_confirm + history (C# TX).",
            History = historyEntry,
            Compute = compute,
            AllocateType = allocateType,
            ByDepartment = byDepartment,
            NumberDigit = numberDigit,
            Include6212 = include6212
        };
    }

    /// <summary>Fail giữa persist và SP → không ghi history thành công (caller ROLLBACK).</summary>
    public static bool ShouldCommitHistory(bool detailsPersisted, bool spSucceeded) =>
        ConfirmIdempotency.ShouldWriteSuccessHistory(detailsPersisted, spSucceeded);
}
