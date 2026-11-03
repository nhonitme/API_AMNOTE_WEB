using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Thứ tự bước G-K B1→B6 và quan hệ phụ thuộc (không đụng B-I).
/// </summary>
public static class GkStepOrder
{
    public static readonly string[] Forward =
    {
        ProductionCostStepCode.FaPrepaidLock,      // B1
        ProductionCostStepCode.CostAllocation,     // B2
        ProductionCostStepCode.ExpTo154Only,       // B3
        ProductionCostStepCode.CostCalc,           // B4
        ProductionCostStepCode.Valuation,          // B5
        ProductionCostStepCode.ProfitLossReport    // B6
    };

    public static int IndexOf(string stepCode)
    {
        var idx = Array.FindIndex(Forward, s =>
            string.Equals(s, stepCode, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            throw new ArgumentException($"STEP_CODE G-K không hợp lệ: {stepCode}");
        return idx;
    }

    public static string Normalize(string stepCode) => Forward[IndexOf(stepCode)];

    /// <summary>Các bước sau <paramref name="stepCode"/> (không gồm chính nó), thuận B1→B6.</summary>
    public static IReadOnlyList<string> Downstream(string stepCode)
    {
        var i = IndexOf(stepCode);
        return Forward.Skip(i + 1).ToList();
    }

    /// <summary>Các bước sau, thứ tự ngược B6→… — dùng khi mở lại cascade.</summary>
    public static IReadOnlyList<string> DownstreamReverse(string stepCode) =>
        Downstream(stepCode).Reverse().ToList();

    /// <summary>Các bước trước và gồm chính nó (B1→target).</summary>
    public static IReadOnlyList<string> UpstreamInclusive(string stepCode)
    {
        var i = IndexOf(stepCode);
        return Forward.Take(i + 1).ToList();
    }

    public static bool IsDone(string? status) =>
        string.Equals(status, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, ProductionCostStepStatus.Previewed, StringComparison.OrdinalIgnoreCase);
}
