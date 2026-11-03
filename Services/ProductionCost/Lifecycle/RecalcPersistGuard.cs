using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// RECALCULATE chỉ Preview / lập RecalcPlan — không Confirm tự động B2–B5,
/// không ghi DryRun tài chính, không overwrite snapshot CONFIRMED.
/// Persist thật = Confirm từng bước trên working version sau REOPEN.
/// </summary>
public static class RecalcPersistGuard
{
    public static void AssertPreviewOnly(RecalcPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.PreviewOnly)
        {
            throw new InvalidOperationException(
                "RECALCULATE không được đánh PreviewOnly=false — không Confirm B2/B3/B4/B5 tự động.");
        }

        if (plan.WouldTouchAccountingOrInventory)
        {
            throw new InvalidOperationException(
                "RECALCULATE Preview không được đụng chứng từ KT / giá kho.");
        }
    }

    public static void AssertNoConfirmRecalculateEndpoint()
    {
        // Cố ý: IProductionCostWriteService không có ConfirmRecalculate.
        // Persist = ConfirmAllocation / ConfirmTransfer / ConfirmCostCalc / ConfirmValuation
        // trên RUN_VERSION làm việc sau REOPEN.
    }

    public static void AssertWritableWorkingVersion(
        string companyCd,
        long runId,
        int workingVersion,
        int headerVersion,
        string? runStatus)
    {
        RunVersionRules.AssertWritableVersion(
            companyCd, runId, workingVersion, headerVersion, runStatus);
    }
}
