using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Transfer;

/// <summary>
/// Exclusive COST_ENGINE giữa G-K và B-I trên cùng COMPANY_CD + PERIOD_YM.
/// Thuần — đối chiếu <c>sp_pc_engine_check</c> / claim (2B).
/// </summary>
public static class CostEngineExclusive
{
    public static readonly HashSet<string> BiCogsRules = new(StringComparer.OrdinalIgnoreCase)
    {
        "154_TO_155",
        "154_TO_155_TO_632",
        "154_TO_632"
    };

    public static void AssertCompatible(string? currentEngine, string requestedEngine, string? cogsRule)
    {
        var current = string.IsNullOrWhiteSpace(currentEngine)
            ? ProductionCostEngine.None
            : currentEngine.Trim().ToUpperInvariant();
        var requested = requestedEngine.Trim().ToUpperInvariant();

        if (requested is not (ProductionCostEngine.Gk or ProductionCostEngine.Bi))
            throw new ArgumentException("COST_ENGINE phải là GK hoặc BI.");

        if (current != ProductionCostEngine.None &&
            !string.Equals(current, requested, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Kỳ này đã thuộc engine khác (hiện tại={current}, yêu cầu={requested}) — exclusive GK/BI.");
        }

        if (string.Equals(requested, ProductionCostEngine.Gk, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(cogsRule, ProductionCostCogsRule.ExpTo154Only, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("G-K chỉ cho phép COGS_RULE=EXP_TO_154_ONLY.");
        }
        else if (!BiCogsRules.Contains(cogsRule ?? string.Empty))
        {
            throw new ArgumentException("B-I COGS_RULE phải là một trong 3 rule cũ (154_TO_155 / 154_TO_155_TO_632 / 154_TO_632).");
        }
    }

    /// <summary>
    /// Chặn B-I full COGS khi kỳ đã claim GK; chặn G-K khi kỳ đã DONE bởi BI.
    /// </summary>
    public static void AssertTransferAllowed(
        string? currentEngine,
        string? currentCogsRule,
        string requestedRule,
        string? biCogsStepStatus)
    {
        if (string.Equals(requestedRule, ProductionCostCogsRule.ExpTo154Only, StringComparison.OrdinalIgnoreCase))
        {
            AssertCompatible(currentEngine, ProductionCostEngine.Gk, ProductionCostCogsRule.ExpTo154Only);

            if (string.Equals(currentEngine, ProductionCostEngine.Bi, StringComparison.OrdinalIgnoreCase) ||
                BiCogsRules.Contains(currentCogsRule ?? string.Empty))
            {
                throw new InvalidOperationException(
                    "Không thể chạy EXP_TO_154_ONLY: kỳ đã thuộc engine B-I / rule B-I.");
            }

            if (string.Equals(biCogsStepStatus, "DONE", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(currentEngine, ProductionCostEngine.Bi, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "COGS_SUMMARY đã DONE bởi B-I — không chạy G-K Bước 3.");
            }

            return;
        }

        if (BiCogsRules.Contains(requestedRule))
        {
            AssertCompatible(currentEngine, ProductionCostEngine.Bi, requestedRule);

            if (string.Equals(currentEngine, ProductionCostEngine.Gk, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentCogsRule, ProductionCostCogsRule.ExpTo154Only, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Không thể chạy rule B-I đầy đủ: kỳ đã claim G-K EXP_TO_154_ONLY.");
            }
        }
    }
}
