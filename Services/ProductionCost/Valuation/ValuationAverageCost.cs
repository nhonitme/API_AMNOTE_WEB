using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Bình quân cuối kỳ / tức thời — port thuần từ InventoryValuationService (không DB, không B-I).
/// </summary>
public static class ValuationAverageCost
{
    public static ValuationAverageResult Compute(
        IEnumerable<ValuationMovementInput> movements,
        string methodCode)
    {
        var method = (methodCode ?? GkValuationMethod.PeriodEndAverage).Trim();
        var list = movements.ToList();
        Cogs632PreviewRules.AssertMovementsSafeForValuation(list);

        return method switch
        {
            GkValuationMethod.PeriodEndAverage => PeriodEndAverage(list),
            GkValuationMethod.MovingAverage => MovingAverage(list),
            _ => throw new ArgumentException(
                $"ValuationMethod không hỗ trợ: {methodCode}. Dùng PERIOD_END_AVG hoặc MOVING_AVG.")
        };
    }

    /// <summary>Áp giá nhập mới theo INPUT_ID vào bản sao movements (Preview after).</summary>
    public static IReadOnlyList<ValuationMovementInput> ApplyReceiptPrices(
        IEnumerable<ValuationMovementInput> movements,
        IEnumerable<ReceiptPricePrepLine> receipts)
    {
        var byInput = receipts
            .Where(r => r.InputId > 0)
            .ToDictionary(r => r.InputId);

        return movements.Select(m =>
        {
            if (!string.Equals(m.FlowType, "INPUT", StringComparison.OrdinalIgnoreCase)
                || m.InputId is null or <= 0
                || !byInput.TryGetValue(m.InputId.Value, out var prep))
            {
                return Clone(m);
            }

            var qty = m.Quantity;
            var newAmount = AllocationMath.RoundDigit(prep.NewUnitPrice * qty, 0);
            return Clone(m, newAmount);
        }).ToList();
    }

    private static ValuationAverageResult PeriodEndAverage(IReadOnlyList<ValuationMovementInput> ordered)
    {
        var openQty = SumQty(ordered, "OPEN");
        var openAmt = SumAmt(ordered, "OPEN");
        var inQty = SumQty(ordered, "INPUT");
        var inAmt = SumAmt(ordered, "INPUT");
        var outQty = SumQty(ordered, "OUTPUT");

        var totalQty = openQty + inQty;
        var totalAmt = openAmt + inAmt;
        var avg = totalQty <= 0 ? 0m : totalAmt / totalQty;
        var endQty = totalQty - outQty;
        var endAmt = Math.Max(0m, endQty * avg);
        var outAmt = Math.Max(0m, outQty * avg);

        return new ValuationAverageResult
        {
            MethodCode = GkValuationMethod.PeriodEndAverage,
            InputQuantity = inQty,
            InputAmount = inAmt,
            OutputQuantity = outQty,
            OutputAmount = AllocationMath.RoundDigit(outAmt, 0),
            EndingQuantity = endQty,
            EndingAmount = AllocationMath.RoundDigit(endAmt, 0),
            EndingAverageUnitCost = avg
        };
    }

    private static ValuationAverageResult MovingAverage(IReadOnlyList<ValuationMovementInput> ordered)
    {
        var currentQty = SumQty(ordered, "OPEN");
        var currentAmt = SumAmt(ordered, "OPEN");
        var currentPrice = currentQty > 0 ? currentAmt / currentQty : 0m;
        decimal inQty = 0, inAmt = 0, outQty = 0, outCost = 0;

        foreach (var m in ordered)
        {
            if (string.Equals(m.FlowType, "OPEN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(m.FlowType, "INPUT", StringComparison.OrdinalIgnoreCase))
            {
                currentQty += m.Quantity;
                currentAmt += m.AmountCc;
                currentPrice = currentQty > 0 ? currentAmt / currentQty : 0m;
                inQty += m.Quantity;
                inAmt += m.AmountCc;
            }
            else if (string.Equals(m.FlowType, "OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                var cost = m.Quantity * currentPrice;
                outQty += m.Quantity;
                outCost += cost;
                currentQty -= m.Quantity;
                currentAmt -= cost;
                if (currentQty <= 0m)
                {
                    currentQty = 0;
                    currentAmt = 0;
                    currentPrice = 0;
                }
                else
                {
                    currentPrice = currentAmt / currentQty;
                }
            }
        }

        return new ValuationAverageResult
        {
            MethodCode = GkValuationMethod.MovingAverage,
            InputQuantity = inQty,
            InputAmount = inAmt,
            OutputQuantity = outQty,
            OutputAmount = AllocationMath.RoundDigit(outCost, 0),
            EndingQuantity = currentQty,
            EndingAmount = AllocationMath.RoundDigit(currentAmt, 0),
            EndingAverageUnitCost = currentQty > 0 ? currentAmt / currentQty : 0m
        };
    }

    private static decimal SumQty(IEnumerable<ValuationMovementInput> rows, string flow) =>
        rows.Where(m => string.Equals(m.FlowType, flow, StringComparison.OrdinalIgnoreCase)).Sum(m => m.Quantity);

    private static decimal SumAmt(IEnumerable<ValuationMovementInput> rows, string flow) =>
        rows.Where(m => string.Equals(m.FlowType, flow, StringComparison.OrdinalIgnoreCase)).Sum(m => m.AmountCc);

    private static ValuationMovementInput Clone(ValuationMovementInput m, decimal? amountCc = null) =>
        new()
        {
            ProductId = m.ProductId,
            DepartmentId = m.DepartmentId,
            InputId = m.InputId,
            InventoryId = m.InventoryId,
            FlowType = m.FlowType,
            Quantity = m.Quantity,
            AmountCc = amountCc ?? m.AmountCc,
            IsExpTo154Only = m.IsExpTo154Only,
            DebitAccountCd = m.DebitAccountCd,
            CreditAccountCd = m.CreditAccountCd,
            CompanyCd = m.CompanyCd
        };
}
