namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

public static class AllocationMath
{
    public static decimal RoundDigit(decimal value, int digit)
    {
        var d = Math.Max(0, digit);
        return Math.Round(value, d, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeReduction(decimal creditAmount, decimal debitAmount154)
        => RoundDigit(creditAmount - debitAmount154, 0);

    public static decimal ComputeAllocationCosts(decimal totalAmount, decimal reduction) =>
        RoundDigit(totalAmount - reduction, 0);
}
