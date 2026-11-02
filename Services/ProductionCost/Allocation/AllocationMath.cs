namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Công thức số học phân bổ 622/627 — bám CONG_THUC_PHAN_BO_622_627 / frmAllocateCosts.
/// Thuần (không DB).
/// </summary>
public static class AllocationMath
{
    /// <summary>Làm tròn theo NUMBER_DIGIT (giống Round WinForms).</summary>
    public static decimal RoundDigit(decimal value, int digit)
    {
        var d = Math.Max(0, digit);
        return Math.Round(value, d, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// reduction(A) = CREDIT_AMOUNT(A) − DEBIT_AMOUNT_154(A).
    /// Riêng company "3619": = CREDIT_AMOUNT(A).
    /// </summary>
    public static decimal ComputeReduction(decimal creditAmount, decimal debitAmount154, string? companyCd = null)
    {
        if (string.Equals(companyCd, "3619", StringComparison.OrdinalIgnoreCase))
            return RoundDigit(creditAmount, 0);
        return RoundDigit(creditAmount - debitAmount154, 0);
    }

    /// <summary>P_A = TOTAL_AMOUNT − REDUCTION.</summary>
    public static decimal ComputeAllocationCosts(decimal totalAmount, decimal reduction) =>
        RoundDigit(totalAmount - reduction, 0);
}
