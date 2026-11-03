using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Đồng bộ RUN_ID/RUN_VERSION request chính với payload DryRun.
/// Confirm sau này phải đọc lại trạng thái/quyền từ DB — không tin client history hint.
/// </summary>
public static class LifecycleRequestGuard
{
    public static void AssertAlignedWithReopen(ProductionCostWriteRequest request, ReopenDryRunPayload dry)
    {
        if (request.RunId is > 0 && request.RunId != dry.RunId)
        {
            throw new InvalidOperationException(
                $"RUN_ID request ({request.RunId}) ≠ ReopenDryRun.RunId ({dry.RunId}).");
        }

        if (request.RunVersion is > 0 && request.RunVersion != dry.CurrentRunVersion)
        {
            throw new InvalidOperationException(
                $"RUN_VERSION request ({request.RunVersion}) ≠ ReopenDryRun.CurrentRunVersion ({dry.CurrentRunVersion}).");
        }
    }

    public static void AssertAlignedWithRecalc(ProductionCostWriteRequest request, RecalcDryRunPayload dry)
    {
        if (request.RunId is > 0 && request.RunId != dry.RunId)
        {
            throw new InvalidOperationException(
                $"RUN_ID request ({request.RunId}) ≠ RecalcDryRun.RunId ({dry.RunId}).");
        }

        if (request.RunVersion is > 0 && request.RunVersion != dry.WorkingRunVersion)
        {
            throw new InvalidOperationException(
                $"RUN_VERSION request ({request.RunVersion}) ≠ RecalcDryRun.WorkingRunVersion ({dry.WorkingRunVersion}).");
        }

        if (dry.WorkingRunVersion != dry.HeaderRunVersion)
        {
            throw new InvalidOperationException(
                "RecalcDryRun.WorkingRunVersion phải khớp HeaderRunVersion hiện hành.");
        }
    }
}
