using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Cấu hình WIP / B4 lấy từ RUN đã khóa (DB) — Confirm không tin WipDryRun client.
/// </summary>
public sealed class WipRunConfig
{
    public string PeriodYm { get; init; } = string.Empty;
    public string WipMethod { get; init; } = ProductionCostWipMethod.PercentCompleted;
    public string IncompleteMode { get; init; } = ProductionCostIncompleteMode.Standard;
    public decimal IncompletePercent { get; init; } = 0.8m;
    public bool EnableProductReduce { get; init; }

    public IncompleteCostConfig ToEngineConfig() =>
        new()
        {
            Mode = IncompleteMode,
            IncompletePercent = IncompletePercent,
            EnableProductReduce = EnableProductReduce,
            IsRoundInventory = true
        };

    public static WipRunConfig FromLockedRun(ProductionCostRunDto lockedRun)
    {
        ArgumentNullException.ThrowIfNull(lockedRun);
        if (string.IsNullOrWhiteSpace(lockedRun.PeriodYm) || lockedRun.PeriodYm.Trim().Length != 6)
            throw new InvalidOperationException("RUN.PERIOD_YM trên DB không hợp lệ.");

        var method = string.IsNullOrWhiteSpace(lockedRun.WipMethod)
            ? ProductionCostWipMethod.PercentCompleted
            : lockedRun.WipMethod.Trim();

        if (!string.Equals(method, ProductionCostWipMethod.PercentCompleted, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(method, ProductionCostWipMethod.ClosingWipQuantity, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"RUN.WIP_METHOD không hợp lệ: {method}.");
        }

        var mode = string.IsNullOrWhiteSpace(lockedRun.IncompleteMode)
            ? ProductionCostIncompleteMode.Standard
            : lockedRun.IncompleteMode.Trim();

        var percent = lockedRun.IncompletePercent;
        if (percent < 0)
            throw new InvalidOperationException($"RUN.INCOMPLETE_PERCENT không hợp lệ: {percent}.");
        if (percent == 0)
            percent = 0.8m;
        // DB có thể lưu 0.8 hoặc 80 — chuẩn hóa về (0,1] cho engine.
        if (percent > 1m)
        {
            if (percent > 100m)
                throw new InvalidOperationException($"RUN.INCOMPLETE_PERCENT không hợp lệ: {percent}.");
            percent /= 100m;
        }

        return new WipRunConfig
        {
            PeriodYm = lockedRun.PeriodYm.Trim(),
            WipMethod = method,
            IncompleteMode = mode,
            IncompletePercent = percent,
            EnableProductReduce = AllocationRunConfig.IsFlagOn(lockedRun.EnableProductReduce)
        };
    }

    /// <summary>
    /// WipDryRun config lệch RUN DB → stale client — chặn trước mọi thay đổi.
    /// </summary>
    public static void AssertClientDryRunAligned(WipRunConfig fromDb, WipDryRunPayload dryRun)
    {
        ArgumentNullException.ThrowIfNull(fromDb);
        ArgumentNullException.ThrowIfNull(dryRun);

        if (!string.Equals(dryRun.WipMethod?.Trim(), fromDb.WipMethod, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"WipMethod client ({dryRun.WipMethod}) ≠ RUN DB ({fromDb.WipMethod}).");
        }

        var clientMode = string.IsNullOrWhiteSpace(dryRun.Config?.Mode)
            ? ProductionCostIncompleteMode.Standard
            : dryRun.Config.Mode.Trim();
        if (!string.Equals(clientMode, fromDb.IncompleteMode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"IncompleteMode client ({clientMode}) ≠ RUN DB ({fromDb.IncompleteMode}).");
        }

        if (dryRun.Config != null && dryRun.Config.EnableProductReduce != fromDb.EnableProductReduce)
        {
            throw new InvalidOperationException(
                $"EnableProductReduce client ({dryRun.Config.EnableProductReduce}) ≠ RUN DB ({fromDb.EnableProductReduce}).");
        }
    }
}
