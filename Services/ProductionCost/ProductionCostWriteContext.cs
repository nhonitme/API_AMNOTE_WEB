using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Kiểm tra danh tính ghi G-K trước khi mở TX / gọi repository.
/// Không chứa DB — dùng chung Confirm (2C-06B+) và unit test.
/// </summary>
public static class ProductionCostWriteContext
{
    public static readonly string[] RequiredHistoryWriteColumns =
    {
        "OPERATION_ID",
        "STABLE_KEY",
        "STEP_CODE",
        "CONTENT_FINGERPRINT"
    };

    public static readonly string[] RequiredHistoryWriteIndexes =
    {
        "uk_pc_hist_stable",
        "uk_pc_hist_operation"
    };

    public static void EnsureCompany(string? companyCd)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new InvalidOperationException("COMPANY_CD is required from authentication context.");
    }

    public static void EnsureDatabase(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException(
                "Database name is required; refusing silent fallback to an unknown company database.");
    }

    public static void EnsureRunIdentity(long runId, int runVersion)
    {
        if (runId <= 0)
            throw new ArgumentException("RUN_ID phải > 0.");
        if (runVersion <= 0)
            throw new ArgumentException("RUN_VERSION phải > 0.");
    }

    public static void EnsureRunIdentity(long? runId, int? runVersion)
    {
        if (runId is null or <= 0)
            throw new ArgumentException("RUN_ID phải > 0.");
        if (runVersion is null or <= 0)
            throw new ArgumentException("RUN_VERSION phải > 0.");
    }

    /// <summary>OperationId bắt buộc cho Confirm / history idempotency.</summary>
    public static string RequireOperationId(string? operationId) =>
        HistoryPlan.RequireOperationId(operationId);

    /// <summary>
    /// Sau SELECT … FOR UPDATE: version request phải khớp header RUN.
    /// </summary>
    public static void AssertLockedRunMatches(
        ProductionCostRunDto lockedRun,
        string companyCd,
        long runId,
        int expectedRunVersion)
    {
        EnsureCompany(companyCd);
        if (lockedRun == null)
            throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");

        if (!string.Equals(lockedRun.CompanyCd, companyCd.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("RUN_ID không thuộc COMPANY_CD.");

        if (lockedRun.RunId != runId)
            throw new InvalidOperationException("RUN_ID khóa không khớp request.");

        if (lockedRun.RunVersion != expectedRunVersion)
        {
            throw new InvalidOperationException(
                $"RUN_VERSION request ({expectedRunVersion}) ≠ header ({lockedRun.RunVersion}).");
        }
    }

    /// <summary>
    /// Sau lock RUN + PERIOD: request phải là CURRENT_RUN_ID và cùng PERIOD_COST_ID.
    /// Chặn Confirm B3 trên RUN không phải current (SP xfer đọc CURRENT_RUN_ID).
    /// </summary>
    public static void AssertPeriodCurrentRunMatches(
        ProductionCostRunDto lockedRun,
        ProductionCostPeriodEngineSnapshot period,
        string companyCd,
        long requestRunId,
        string requestPeriodYm)
    {
        EnsureCompany(companyCd);
        if (lockedRun == null)
            throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
        if (period == null)
            throw new InvalidOperationException("production_cost_period không tồn tại cho PERIOD_YM.");

        if (!string.Equals(period.CompanyCd, companyCd.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PERIOD không thuộc COMPANY_CD.");

        var periodYm = (period.PeriodYm ?? string.Empty).Trim();
        if (!string.Equals(periodYm, requestPeriodYm.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PERIOD_YM period ({period.PeriodYm}) ≠ request ({requestPeriodYm.Trim()}).");
        }

        if (!string.Equals((lockedRun.PeriodYm ?? string.Empty).Trim(), periodYm, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PERIOD_YM RUN ({lockedRun.PeriodYm}) ≠ period ({period.PeriodYm}).");
        }

        if (period.CurrentRunId is null or <= 0)
            throw new InvalidOperationException("PERIOD thiếu CURRENT_RUN_ID — không Confirm B3.");

        if (period.CurrentRunId.Value != requestRunId)
        {
            throw new InvalidOperationException(
                $"RUN_ID request ({requestRunId}) ≠ PERIOD.CURRENT_RUN_ID ({period.CurrentRunId.Value}) — " +
                "không Confirm / không đánh STEP B3 DONE cho RUN không phải current " +
                "(sp_period_lock_cogs_build_xfer dùng CURRENT_RUN_ID).");
        }

        if (lockedRun.RunId != period.CurrentRunId.Value)
        {
            throw new InvalidOperationException(
                $"RUN khóa ({lockedRun.RunId}) ≠ PERIOD.CURRENT_RUN_ID ({period.CurrentRunId.Value}).");
        }

        if (lockedRun.PeriodCostId != period.PeriodCostId)
        {
            throw new InvalidOperationException(
                $"PERIOD_COST_ID RUN ({lockedRun.PeriodCostId}) ≠ period ({period.PeriodCostId}).");
        }
    }

    public static void AssertHistoryWriteSchemaReady(ProductionCostHistoryWriteSchemaProbe probe)
    {
        if (probe == null || !probe.Ready)
        {
            var missing = probe?.MissingColumns is { Count: > 0 }
                ? string.Join(", ", probe.MissingColumns)
                : "OPERATION_ID/STABLE_KEY/STEP_CODE";
            throw new InvalidOperationException(
                "Schema production_cost_history chưa sẵn sàng ghi (thiếu " + missing +
                "). SOURCE 20261103_2c06a_ddl_history_operation.sql trên DEV trước khi EnableWrites.");
        }
    }

    public static ProductionCostHistoryInsert FromHistoryPlan(
        HistoryEntryPlan plan,
        string companyCd,
        string periodYm)
    {
        EnsureCompany(companyCd);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        var operationId = RequireOperationId(plan.OperationId);
        HistoryPlan.EnsureStableKeyLength(plan.StableKey);

        return new ProductionCostHistoryInsert
        {
            CompanyCd = companyCd.Trim(),
            PeriodYm = periodYm.Trim(),
            RunId = plan.RunId,
            RunVersion = plan.RunVersionAfter ?? plan.RunVersionBefore,
            StepIndex = plan.StepIndex,
            StepCode = plan.StepCode,
            ActionCode = plan.ActionCode,
            OperationId = operationId,
            StableKey = plan.StableKey.Trim(),
            StatusAfter = plan.StatusAfter,
            Reason = plan.Reason,
            UserName = plan.UserId,
            ActionAt = plan.ActionAtUtc ?? DateTime.UtcNow
        };
    }
}
