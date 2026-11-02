using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>Tiền đề Bước 5 VALUATION — B2/B3/B4 DONE cùng RUN_ID+VERSION.</summary>
public static class ValuationPrerequisite
{
    public static void AssertReady(
        string companyCd,
        string periodYm,
        long? runId,
        int? runVersion,
        string? runStatus,
        string? allocationStepStatus,
        string? expTo154StepStatus,
        string? costCalcStepStatus)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new InvalidOperationException("COMPANY_CD bắt buộc.");
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        if (runId is null or <= 0 || runVersion is null or <= 0)
            throw new InvalidOperationException("VALUATION yêu cầu RUN_ID + RUN_VERSION hiện tại.");

        if (string.Equals(runStatus, GkRunStatus.Superseded, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Không áp dụng giá từ phiên SUPERSEDED.");

        AssertStepDone(allocationStepStatus, ProductionCostStepCode.CostAllocation, "Bước 2");
        AssertStepDone(expTo154StepStatus, ProductionCostStepCode.ExpTo154Only, "Bước 3");
        AssertStepDone(costCalcStepStatus, ProductionCostStepCode.CostCalc, "Bước 4");
    }

    public static bool IsStepDone(string? status) =>
        string.Equals(status, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase);

    private static void AssertStepDone(string? status, string stepCode, string label)
    {
        if (!IsStepDone(status))
        {
            throw new InvalidOperationException(
                $"{label} ({stepCode}) phải = DONE trước VALUATION.");
        }
    }

    /// <summary>Sau Confirm B5: VALUATION=DONE — không tự CONFIRMED toàn kỳ.</summary>
    public static string AfterValuationConfirm(string? currentStatus) =>
        GkRunStatus.AfterCostCalcConfirm(currentStatus);
}
