using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Engine tính dở dang / giá thành thành phẩm — port từ CONG_THUC_TINH_DO_DANG /
/// frmCalculateProductProcess / wipCostEngine.ts. Thuần C#, không DB.
/// </summary>
public static class WipCostEngine
{
    public static readonly IncompleteCostConfig DefaultConfig = new();

    public static decimal Before154(WipCalcInput input) =>
        AllocationMath.RoundDigit(input.BeforeAmountResult621 + input.BeforeAmountResult62N, 0);

    public static decimal TotalIncurred(WipCalcInput input, IncompleteCostConfig config)
    {
        var baseAmt = config.Mode == ProductionCostIncompleteMode.Standard
            ? input.Amount621 + input.Amount6212 + input.Amount622 + input.Amount627
            : AllocationMath.RoundDigit(
                input.Amount621 + input.Amount6212 + input.Amount622 + input.Amount627, 0);
        // 154 direct cộng đúng 1 lần — không gồm bút toán kết chuyển B3.
        return AllocationMath.RoundDigit(baseAmt + input.Amount154Direct, 0);
    }

    public static decimal CalcClosingByPercent(
        decimal beforeAmount154,
        decimal amount621,
        decimal percentCompleted,
        bool isRoundInventory)
    {
        var raw = (beforeAmount154 + amount621) * ((100m - percentCompleted) / 100m);
        return isRoundInventory ? AllocationMath.RoundDigit(raw, 0) : raw;
    }

    public static (decimal Closing, string? Error) CalcClosingByQuantity(
        decimal beforeAmount154,
        decimal amount621,
        decimal quantity,
        decimal closingQty,
        bool showAllWhenQtyZero,
        decimal previousClosing154,
        bool isRoundInventory)
    {
        if (closingQty < 0)
            return (0, "Số lượng dở dang cuối kỳ không được âm.");
        if (closingQty > 0)
        {
            var denom = quantity + closingQty;
            if (denom == 0)
                return (0, "Tổng số lượng (Q + Qdd) = 0 — không chia được.");
            var raw = (beforeAmount154 + amount621) / denom * closingQty;
            return (isRoundInventory ? AllocationMath.RoundDigit(raw, 0) : raw, null);
        }

        if (showAllWhenQtyZero && quantity <= 0)
            return (previousClosing154, null);
        return (0, null);
    }

    public static (decimal Percent, string? Error) ReversePercentFromClosing(
        decimal closing154,
        decimal beforeAmount154,
        decimal amount621)
    {
        var bas = beforeAmount154 + amount621;
        if (bas == 0)
            return (0, "ĐK 154 + 621 = 0 — không tính ngược % hoàn thành.");
        var percent = 100m - closing154 / bas * 100m;
        return (AllocationMath.RoundDigit(percent, 2), null);
    }

    public static WipCalcResult CalculateRow(
        WipCalcInput input,
        string wipMethod,
        IncompleteCostConfig? config = null)
    {
        config ??= DefaultConfig;
        var errors = new List<string>();
        var percentCompleted = input.PercentCompleted;
        var closingIncompleteQty = input.ClosingIncompleteQty;
        var b154 = Before154(input);
        var incurred = TotalIncurred(input, config);
        var method = (wipMethod ?? ProductionCostWipMethod.PercentCompleted).Trim();

        if (string.Equals(method, ProductionCostWipMethod.PercentCompleted, StringComparison.OrdinalIgnoreCase))
        {
            if (percentCompleted is < 0 or > 100)
            {
                errors.Add($"% hoàn thành {input.ProductCd} phải trong [0, 100].");
                percentCompleted = 0;
            }
        }
        else if (string.Equals(method, ProductionCostWipMethod.ClosingWipQuantity, StringComparison.OrdinalIgnoreCase))
        {
            if (closingIncompleteQty < 0)
                errors.Add($"SL dở dang cuối kỳ {input.ProductCd} không được âm.");
        }
        else
        {
            throw new ArgumentException(
                $"WipMethod không hợp lệ: {wipMethod}. Dùng PERCENT_COMPLETED hoặc CLOSING_WIP_QUANTITY.");
        }

        decimal amountResult621 = 0;
        decimal amountResult62N = 0;
        decimal closing = 0;
        var qHt = input.Quantity;
        var productReduce = config.EnableProductReduce ? input.ProductReduce : 0m;

        if (string.Equals(config.Mode, ProductionCostIncompleteMode.SlTpSx, StringComparison.OrdinalIgnoreCase))
        {
            var special = CalcSpecialSlTpSx(
                CloneWithQty(input, Math.Max(0, closingIncompleteQty)), config);
            amountResult621 = special.AmountResult621;
            amountResult62N = special.AmountResult62N;
            closing = AllocationMath.RoundDigit(amountResult621 + amountResult62N, 0);
            qHt = special.QHt;
        }
        else if (string.Equals(config.Mode, ProductionCostIncompleteMode.Id2720, StringComparison.OrdinalIgnoreCase))
        {
            var special = CalcSpecialId2720(
                CloneWithQty(input, Math.Max(0, closingIncompleteQty)), config);
            amountResult621 = special.AmountResult621;
            amountResult62N = special.AmountResult62N;
            closing = AllocationMath.RoundDigit(amountResult621 + amountResult62N, 0);
            qHt = special.QHt;
        }
        else if (string.Equals(method, ProductionCostWipMethod.PercentCompleted, StringComparison.OrdinalIgnoreCase))
        {
            if (input.ClosingAmountOverride is { } ov)
            {
                closing = config.IsRoundInventory ? AllocationMath.RoundDigit(ov, 0) : ov;
                var rev = ReversePercentFromClosing(closing, b154, input.Amount621);
                if (rev.Error != null) errors.Add(rev.Error);
                else percentCompleted = rev.Percent;
                if (percentCompleted is < 0 or > 100)
                    errors.Add($"% hoàn thành suy ra ngoài [0, 100] cho {input.ProductCd}.");
            }
            else
            {
                closing = CalcClosingByPercent(b154, input.Amount621, percentCompleted, config.IsRoundInventory);
            }

            amountResult621 = closing;
            amountResult62N = 0;
        }
        else
        {
            var qtyRes = CalcClosingByQuantity(
                b154, input.Amount621, input.Quantity, closingIncompleteQty,
                config.ShowAllWhenQtyZero, input.PreviousClosing154, config.IsRoundInventory);
            if (qtyRes.Error != null) errors.Add(qtyRes.Error);
            closing = qtyRes.Closing;
            amountResult621 = closing;
            amountResult62N = 0;
        }

        if (closing < 0)
            errors.Add($"Dở dang cuối kỳ {input.ProductCd} âm.");
        var maxCost = b154 + incurred;
        if (closing > maxCost + 0.0001m)
            errors.Add($"Dở dang cuối kỳ {input.ProductCd} lớn hơn tổng chi phí hợp lệ.");

        decimal totalCostPrice;
        if (string.Equals(config.Mode, ProductionCostIncompleteMode.Id2720, StringComparison.OrdinalIgnoreCase))
        {
            totalCostPrice = AllocationMath.RoundDigit(
                input.BeforeAmountResult621 + input.BeforeAmountResult62N + incurred - closing, 0);
        }
        else if (string.Equals(config.Mode, ProductionCostIncompleteMode.SlTpSx, StringComparison.OrdinalIgnoreCase))
        {
            totalCostPrice = AllocationMath.RoundDigit(b154 + incurred - closing - productReduce, 0);
        }
        else
        {
            totalCostPrice = AllocationMath.RoundDigit(b154 + incurred - closing, 0);
        }

        if (totalCostPrice < 0)
            errors.Add($"Tổng giá thành {input.ProductCd} âm — dữ liệu không hợp lệ.");

        var divisor =
            string.Equals(config.Mode, ProductionCostIncompleteMode.SlTpSx, StringComparison.OrdinalIgnoreCase)
            || string.Equals(config.Mode, ProductionCostIncompleteMode.Id2720, StringComparison.OrdinalIgnoreCase)
                ? qHt
                : input.Quantity;

        decimal unitCost = 0;
        if (divisor > 0)
            unitCost = totalCostPrice / divisor;
        else if (totalCostPrice != 0)
            errors.Add($"SL hoàn thành {input.ProductCd} = 0 — không chia đơn giá.");

        return new WipCalcResult
        {
            ProductId = input.ProductId,
            DepartmentId = input.DepartmentId,
            ProductCd = input.ProductCd,
            DepartmentCd = input.DepartmentCd,
            IncompleteQtyOpening = input.IncompleteQtyOpening,
            BeforeAmountResult621 = input.BeforeAmountResult621,
            BeforeAmountResult62N = input.BeforeAmountResult62N,
            BeforeAmount154 = b154,
            Amount621 = input.Amount621,
            Amount6212 = input.Amount6212,
            Amount622 = input.Amount622,
            Amount627 = input.Amount627,
            Amount154Direct = input.Amount154Direct,
            TotalAmountIncurred = incurred,
            Quantity = input.Quantity,
            PercentCompleted = percentCompleted,
            ClosingIncompleteQty = closingIncompleteQty,
            AmountResult621 = amountResult621,
            AmountResult62N = amountResult62N,
            ClosingIncompleteAmount154 = closing,
            ProductReduce = productReduce,
            TotalCostPrice = totalCostPrice,
            UnitCost = unitCost,
            QHt = qHt,
            Errors = errors
        };
    }

    /// <summary>Tính nhiều dòng; nếu có Sources thì gộp vào Amount* (loại XFER).</summary>
    public static IReadOnlyList<WipCalcResult> Run(
        IEnumerable<WipCalcInput> products,
        string wipMethod,
        IncompleteCostConfig? config = null,
        IEnumerable<CostSourceLine>? sources = null)
    {
        config ??= DefaultConfig;
        var sourceList = sources?.ToList() ?? new List<CostSourceLine>();
        if (sourceList.Count > 0)
            CostSourceRules.AssertNoTransferInIncurred(sourceList);

        var results = new List<WipCalcResult>();
        foreach (var product in products)
        {
            var input = sourceList.Count > 0
                ? CostSourceRules.ApplySourcesToProduct(product, sourceList)
                : product;
            // Khi có sources: vẫn giữ Amount154Direct từ product nếu source không có dòng DIRECT
            // ApplySourcesToProduct đã Pick — OK.
            results.Add(CalculateRow(input, wipMethod, config));
        }

        return results;
    }

    private static WipCalcInput CloneWithQty(WipCalcInput input, decimal closingQty) =>
        new()
        {
            ProductId = input.ProductId,
            ProductCd = input.ProductCd,
            DepartmentId = input.DepartmentId,
            DepartmentCd = input.DepartmentCd,
            IncompleteQtyOpening = input.IncompleteQtyOpening,
            BeforeAmountResult621 = input.BeforeAmountResult621,
            BeforeAmountResult62N = input.BeforeAmountResult62N,
            Amount621 = input.Amount621,
            Amount6212 = input.Amount6212,
            Amount622 = input.Amount622,
            Amount627 = input.Amount627,
            Amount154Direct = input.Amount154Direct,
            Quantity = input.Quantity,
            PercentCompleted = input.PercentCompleted,
            ClosingIncompleteQty = closingQty,
            ClosingAmountOverride = input.ClosingAmountOverride,
            ProductReduce = input.ProductReduce,
            PreviousClosing154 = input.PreviousClosing154
        };

    private static (decimal AmountResult621, decimal AmountResult62N, decimal QHt) CalcSpecialSlTpSx(
        WipCalcInput input,
        IncompleteCostConfig config)
    {
        var p = config.IncompletePercent;
        var d = config.SpecialRoundDigit;
        var Q = input.Quantity;
        var Qdd = input.ClosingIncompleteQty;
        var Qdk = input.IncompleteQtyOpening;
        var hasDk = Qdk > 0;
        var hasCk = Qdd > 0;

        if (!hasDk && !hasCk)
            return (0, 0, Q);

        if (hasDk && hasCk)
        {
            var qHt = Q + Qdk - Qdd;
            var denom621 = qHt + Qdd;
            var amountResult621 = denom621 == 0
                ? 0
                : AllocationMath.RoundDigit((input.BeforeAmountResult621 + input.Amount621) / denom621 * Qdd, d);
            var denom62N = qHt + Qdd * p;
            var amountResult62N = denom62N == 0
                ? 0
                : AllocationMath.RoundDigit(
                    (input.BeforeAmountResult62N + input.Amount6212 + input.Amount622 + input.Amount627)
                    / denom62N * Qdd * p, d);
            return (amountResult621, amountResult62N, qHt);
        }

        // Không ĐK, có CK
        var qHt2 = Q - Qdd;
        var amountResult621B = Q == 0 ? 0 : AllocationMath.RoundDigit(input.Amount621 / Q * Qdd, d);
        var denom62Nb = qHt2 + p * Qdd;
        var amountResult62Nb = denom62Nb == 0
            ? 0
            : AllocationMath.RoundDigit(
                (Before154(input) + input.Amount6212 + input.Amount622 + input.Amount627)
                / denom62Nb * Qdd * p, d);
        return (amountResult621B, amountResult62Nb, qHt2);
    }

    private static (decimal AmountResult621, decimal AmountResult62N, decimal QHt) CalcSpecialId2720(
        WipCalcInput input,
        IncompleteCostConfig config)
    {
        var p = config.IncompletePercent;
        var d = config.SpecialRoundDigit;
        var Q = input.Quantity;
        var Qdd = input.ClosingIncompleteQty;
        var denom621 = Q + Qdd;
        var amountResult621 = denom621 == 0
            ? 0
            : AllocationMath.RoundDigit((input.BeforeAmountResult621 + input.Amount621) / denom621 * Qdd, d);
        var denom62N = Q + Qdd * p;
        var amountResult62N = denom62N == 0
            ? 0
            : AllocationMath.RoundDigit(
                (input.BeforeAmountResult62N + input.Amount6212 + input.Amount622 + input.Amount627)
                / denom62N * Qdd * p, d);
        return (amountResult621, amountResult62N, Q);
    }
}
