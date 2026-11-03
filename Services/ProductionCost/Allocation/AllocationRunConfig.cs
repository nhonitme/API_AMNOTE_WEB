using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Cấu hình phân bổ B2 lấy từ RUN đã khóa (DB) — Confirm không tin DryRun client.
/// </summary>
public sealed class AllocationRunConfig
{
    public string PeriodYm { get; init; } = string.Empty;
    public int AllocateType { get; init; }
    public bool ByDepartment { get; init; }
    public int NumberDigit { get; init; }
    public bool Include6212 { get; init; }

    public static AllocationRunConfig FromLockedRun(ProductionCostRunDto lockedRun)
    {
        ArgumentNullException.ThrowIfNull(lockedRun);
        if (string.IsNullOrWhiteSpace(lockedRun.PeriodYm) || lockedRun.PeriodYm.Trim().Length != 6)
            throw new InvalidOperationException("RUN.PERIOD_YM trên DB không hợp lệ.");

        var allocateType = lockedRun.AllocateType;
        if (allocateType is not (0 or 1))
            throw new InvalidOperationException($"RUN.ALLOCATE_TYPE không hợp lệ: {allocateType}.");

        return new AllocationRunConfig
        {
            PeriodYm = lockedRun.PeriodYm.Trim(),
            AllocateType = allocateType,
            ByDepartment = IsFlagOn(lockedRun.Is622627ByDepartment),
            NumberDigit = lockedRun.AllocNumberDigit,
            Include6212 = IsFlagOn(lockedRun.Is6212)
        };
    }

    /// <summary>request.PeriodYm phải khớp PERIOD_YM của RUN đã khóa.</summary>
    public static void AssertPeriodYmMatches(ProductionCostRunDto lockedRun, string requestPeriodYm)
    {
        ArgumentNullException.ThrowIfNull(lockedRun);
        if (string.IsNullOrWhiteSpace(requestPeriodYm) || requestPeriodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM request phải 6 ký tự.");

        var locked = (lockedRun.PeriodYm ?? string.Empty).Trim();
        var request = requestPeriodYm.Trim();
        if (!string.Equals(locked, request, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PERIOD_YM request ({request}) ≠ RUN đã khóa ({locked}).");
        }
    }

    /// <summary>
    /// DryRun config lệch RUN DB → stale client — chặn trước mọi thay đổi.
    /// Nguồn tin cậy = RUN; DryRun chỉ được khớp, không ghi đè.
    /// </summary>
    public static void AssertClientDryRunAligned(AllocationRunConfig fromDb, AllocationDryRunPayload dryRun)
    {
        ArgumentNullException.ThrowIfNull(fromDb);
        ArgumentNullException.ThrowIfNull(dryRun);

        if (dryRun.AllocateType != fromDb.AllocateType)
        {
            throw new InvalidOperationException(
                $"AllocateType client ({dryRun.AllocateType}) ≠ RUN DB ({fromDb.AllocateType}).");
        }

        if (dryRun.ByDepartment != fromDb.ByDepartment)
        {
            throw new InvalidOperationException(
                $"ByDepartment client ({dryRun.ByDepartment}) ≠ RUN DB ({fromDb.ByDepartment}).");
        }

        if (dryRun.NumberDigit != fromDb.NumberDigit)
        {
            throw new InvalidOperationException(
                $"NumberDigit client ({dryRun.NumberDigit}) ≠ RUN DB ({fromDb.NumberDigit}).");
        }

        if (dryRun.Include6212 != fromDb.Include6212)
        {
            throw new InvalidOperationException(
                $"Include6212 client ({dryRun.Include6212}) ≠ RUN DB ({fromDb.Include6212}).");
        }
    }

    public static bool IsFlagOn(string? flag) =>
        string.Equals(flag?.Trim(), "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(flag?.Trim(), "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(flag?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
