using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Transfer;

/// <summary>
/// Quy tắc Bước 3 G-K: chỉ EXP_TO_154_ONLY — không tạo bút toán giá thành / xuất kho downstream.
/// </summary>
public static class ExpTo154Rules
{
    /// <summary>Có được phép khi Nợ 154* (kể cả TK chi tiết 6211, 6221, …).</summary>
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

    /// <summary>true nếu bút toán là kết chuyển hợp lệ Nợ 154* / Có 621*|622*|623*|627*.</summary>
    public static bool IsValidExpLine(string? debit, string? credit)
    {
        if (!TryNormalizeAccount(debit, out var d) || !TryNormalizeAccount(credit, out var c))
            return false;
        if (!StartsWithPrefix(d, "154"))
            return false;
        // Không cho N154/C155|C632 lẫn vào “hợp lệ”
        if (IsForbiddenDownstreamLine(d, c))
            return false;
        return AllowedCreditPrefixes.Any(p => StartsWithPrefix(c, p));
    }

    /// <summary>
    /// Bút toán cấm ở Step 3 (cả TK tổng và chi tiết):
    /// - N155 / C154 (nhập TP từ 154)
    /// - N632 / C155 (giá vốn từ TP)
    /// - N632 / C154 (giá vốn từ dở dang)
    /// - N154 / C155 | C632 ; N155 / C632
    /// </summary>
    public static bool IsForbiddenDownstreamLine(string? debit, string? credit)
    {
        if (!TryNormalizeAccount(debit, out var d) || !TryNormalizeAccount(credit, out var c))
            return false;

        // N155*/C154*
        if (StartsWithPrefix(d, "155") && StartsWithPrefix(c, "154"))
            return true;
        // N632*/C155*
        if (StartsWithPrefix(d, "632") && StartsWithPrefix(c, "155"))
            return true;
        // N632*/C154*
        if (StartsWithPrefix(d, "632") && StartsWithPrefix(c, "154"))
            return true;
        // N154*/C155* hoặc N154*/C632*
        if (StartsWithPrefix(d, "154") && (StartsWithPrefix(c, "155") || StartsWithPrefix(c, "632")))
            return true;
        // N155*/C632*
        if (StartsWithPrefix(d, "155") && StartsWithPrefix(c, "632"))
            return true;

        return false;
    }

    public static void AssertLinesAllowed(IEnumerable<(string? Debit, string? Credit)> lines)
    {
        foreach (var (debit, credit) in lines)
        {
            if (IsForbiddenDownstreamLine(debit, credit))
            {
                throw new InvalidOperationException(
                    $"Bước 3 cấm bút toán downstream Nợ {debit} / Có {credit} " +
                    "(không tạo N155/C154, N632/C155, N632/C154).");
            }
        }
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

    private static bool TryNormalizeAccount(string? account, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(account))
            return false;
        normalized = account.Trim();
        return normalized.Length > 0;
    }

    private static bool StartsWithPrefix(string account, string prefix) =>
        account.StartsWith(prefix, StringComparison.Ordinal);
}
