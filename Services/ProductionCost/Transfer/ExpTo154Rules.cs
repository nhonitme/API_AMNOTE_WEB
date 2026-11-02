using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Transfer;

/// <summary>
/// Quy tắc Bước 3 G-K: chỉ EXP_TO_154_ONLY — không 154→155 / 154→632.
/// </summary>
public static class ExpTo154Rules
{
    public static readonly string[] AllowedCreditPrefixes = { "621", "622", "623", "627" };

    public static void AssertRuleCode(string? ruleCode)
    {
        if (!string.Equals(ruleCode, ProductionCostCogsRule.ExpTo154Only, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Bước 3 G-K chỉ cho phép COGS_RULE=EXP_TO_154_ONLY.");
    }

    public static void AssertNoDownstreamTransfer(string? ruleCode)
    {
        AssertRuleCode(ruleCode);
        if (CostEngineExclusive.BiCogsRules.Contains(ruleCode ?? string.Empty))
            throw new InvalidOperationException("Không được thực hiện 154→155 hoặc 154→632 trong Bước 3 G-K.");
    }

    /// <summary>true nếu bút toán là kết chuyển hợp lệ Nợ 154 / Có 621|622|623|627.</summary>
    public static bool IsValidExpLine(string? debit, string? credit)
    {
        if (string.IsNullOrWhiteSpace(debit) || string.IsNullOrWhiteSpace(credit))
            return false;
        var d = debit.Trim();
        var c = credit.Trim();
        if (!d.StartsWith("154", StringComparison.Ordinal))
            return false;
        return AllowedCreditPrefixes.Any(p => c.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>Phát hiện dòng downstream cấm trong voucher EXP_TO_154_ONLY.</summary>
    public static bool IsForbiddenDownstreamLine(string? debit, string? credit)
    {
        if (string.IsNullOrWhiteSpace(debit) || string.IsNullOrWhiteSpace(credit))
            return false;
        var d = debit.Trim();
        var c = credit.Trim();
        // 154→155 hoặc 154→632 (hoặc 155→632)
        if (d.StartsWith("154", StringComparison.Ordinal) &&
            (c.StartsWith("155", StringComparison.Ordinal) || c.StartsWith("632", StringComparison.Ordinal)))
            return true;
        if (d.StartsWith("155", StringComparison.Ordinal) && c.StartsWith("632", StringComparison.Ordinal))
            return true;
        return false;
    }

    public static void AssertAllocationConfirmed(string? allocationStepStatus, decimal confirmedAllocTotal)
    {
        if (!string.Equals(allocationStepStatus, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bước 3 yêu cầu phân bổ 622/627 đã CONFIRMED (COST_ALLOCATION=DONE).");
        if (confirmedAllocTotal <= 0m)
            throw new InvalidOperationException("Chưa có kết quả phân bổ 622/627 CONFIRMED để đối chiếu nguồn.");
    }

    public static string StableKey(string companyCd, string periodYm) =>
        $"{ProductionCostCogsRule.ExpTo154Only}:{companyCd}:{periodYm}";
}
