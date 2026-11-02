using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Bình quân cuối kỳ / tức thời theo nhóm tồn kho — khớp InventoryValuationService
/// (PRODUCT + STORE + CHIT_TYPE; sort TXN_YMD → INVENTORY_ID → CHITDETAIL_ID).
/// Không đổi nghiệp vụ InventoryValuation / B-I.
/// </summary>
public static class ValuationAverageCost
{
    public static ValuationGroupKey GroupKeyOf(ValuationMovementInput m) => new()
    {
        CompanyCd = (m.CompanyCd ?? string.Empty).Trim(),
        ProductId = m.ProductId,
        ProductCd = (m.ProductCd ?? string.Empty).Trim(),
        StoreId = m.StoreId ?? 0,
        StoreCd = (m.StoreCd ?? string.Empty).Trim(),
        ChitType = (m.ChitType ?? string.Empty).Trim()
    };

    /// <summary>
    /// Sắp xếp giống InventoryValuationService.RunCalculationAsync:
    /// PRODUCT_CD → STORE_CD → TXN_YMD → INVENTORY_ID → CHITDETAIL_ID.
    /// </summary>
    public static IReadOnlyList<ValuationMovementInput> OrderLikeInventoryEngine(
        IEnumerable<ValuationMovementInput> movements) =>
        movements
            .OrderBy(m => m.ProductCd ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.ProductId)
            .ThenBy(m => m.StoreCd ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.StoreId ?? 0)
            .ThenBy(m => m.TxnYmd ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(m => m.InventoryId ?? 0)
            .ThenBy(m => m.ChitdetailId ?? 0)
            .ToList();

    public static IReadOnlyList<ValuationAverageResult> ComputeByGroup(
        IEnumerable<ValuationMovementInput> movements,
        string methodCode)
    {
        var method = (methodCode ?? GkValuationMethod.PeriodEndAverage).Trim();
        if (method is not (GkValuationMethod.PeriodEndAverage or GkValuationMethod.MovingAverage))
        {
            throw new ArgumentException(
                $"ValuationMethod không hỗ trợ: {methodCode}. Dùng PERIOD_END_AVG hoặc MOVING_AVG.");
        }

        var list = movements.ToList();
        Cogs632PreviewRules.AssertMovementsSafeForValuation(list);

        if (method == GkValuationMethod.MovingAverage)
            AssertMovingAvgSortFields(list);

        var ordered = OrderLikeInventoryEngine(list);
        var results = new List<ValuationAverageResult>();

        foreach (var group in ordered.GroupBy(GroupKeyOf))
        {
            var rows = group.ToList();
            var key = group.Key;
            if (key.ProductId <= 0)
                throw new InvalidOperationException("Nhóm định giá thiếu PRODUCT_ID hợp lệ.");

            var result = method == GkValuationMethod.PeriodEndAverage
                ? PeriodEndAverage(rows, key)
                : MovingAverage(rows, key);
            results.Add(result);
        }

        return results;
    }

    /// <summary>Tương thích gọi đơn nhóm — ném lỗi nếu nhiều nhóm.</summary>
    public static ValuationAverageResult Compute(
        IEnumerable<ValuationMovementInput> movements,
        string methodCode)
    {
        var all = ComputeByGroup(movements, methodCode);
        if (all.Count == 0)
            throw new InvalidOperationException("Không có nhóm định giá để tính.");
        if (all.Count > 1)
        {
            throw new InvalidOperationException(
                $"Có {all.Count} nhóm định giá — dùng ComputeByGroup, không gộp ProductId=0.");
        }

        return all[0];
    }

    /// <summary>
    /// Áp giá nhập mới theo INPUT_ID — chỉ INPUT cùng COMPANY_CD + PRODUCT_ID + qty khớp.
    /// </summary>
    public static IReadOnlyList<ValuationMovementInput> ApplyReceiptPrices(
        IEnumerable<ValuationMovementInput> movements,
        IEnumerable<ReceiptPricePrepLine> receipts,
        string companyCd,
        IReadOnlyList<WipCalcResult>? confirmedWip = null)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new ArgumentException("COMPANY_CD bắt buộc khi ApplyReceiptPrices.");

        var movementList = movements.ToList();
        var receiptList = receipts.ToList();
        var byInput = new Dictionary<long, ReceiptPricePrepLine>();
        foreach (var r in receiptList)
        {
            if (r.InputId <= 0)
                throw new ArgumentException("INPUT_ID không hợp lệ trong receipt prep.");
            if (!byInput.TryAdd(r.InputId, r))
                throw new InvalidOperationException($"INPUT_ID trùng khi áp giá: {r.InputId}.");
            if (!string.IsNullOrWhiteSpace(r.CompanyCd) &&
                !string.Equals(r.CompanyCd, companyCd, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"INPUT_ID={r.InputId} COMPANY_CD={r.CompanyCd} khác kỳ định giá {companyCd}.");
            }
        }

        var wipKeys = confirmedWip?
            .Select(w => (w.ProductId, w.DepartmentId))
            .ToHashSet();

        var appliedInputs = new HashSet<long>();
        var result = new List<ValuationMovementInput>(movementList.Count);

        foreach (var m in movementList)
        {
            if (!string.Equals(m.FlowType, "INPUT", StringComparison.OrdinalIgnoreCase)
                || m.InputId is null or <= 0
                || !byInput.TryGetValue(m.InputId.Value, out var prep))
            {
                result.Add(Clone(m));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(m.CompanyCd) &&
                !string.Equals(m.CompanyCd, companyCd, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Movement INPUT_ID={m.InputId} thuộc COMPANY_CD={m.CompanyCd}, không phải {companyCd}.");
            }

            if (m.ProductId != prep.ProductId)
            {
                throw new InvalidOperationException(
                    $"INPUT_ID={prep.InputId}: PRODUCT_ID movement ({m.ProductId}) ≠ receipt ({prep.ProductId}).");
            }

            if (prep.DepartmentId > 0 && wipKeys != null &&
                !wipKeys.Contains((prep.ProductId, prep.DepartmentId)))
            {
                throw new InvalidOperationException(
                    $"INPUT_ID={prep.InputId}: không thuộc đối tượng GT PRODUCT={prep.ProductId} DEPT={prep.DepartmentId}.");
            }

            if (Math.Abs(m.Quantity - prep.Qty) > ReceiptReconcile.QtyTolerance)
            {
                throw new InvalidOperationException(
                    $"INPUT_ID={prep.InputId}: QTY movement ({m.Quantity}) ≠ receipt ({prep.Qty}).");
            }

            appliedInputs.Add(prep.InputId);
            var newAmount = AllocationMath.RoundDigit(prep.NewUnitPrice * m.Quantity, 0);
            result.Add(Clone(m, newAmount));
        }

        foreach (var r in receiptList)
        {
            if (!appliedInputs.Contains(r.InputId))
            {
                throw new InvalidOperationException(
                    $"INPUT_ID={r.InputId} không thuộc kỳ/nhóm movements cần cập nhật giá.");
            }
        }

        return result;
    }

    private static void AssertMovingAvgSortFields(IReadOnlyList<ValuationMovementInput> list)
    {
        foreach (var m in list.Where(x =>
                     string.Equals(x.FlowType, "INPUT", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(x.FlowType, "OUTPUT", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(m.TxnYmd) || m.TxnYmd.Length != 8)
            {
                throw new InvalidOperationException(
                    $"MOVING_AVG yêu cầu TXN_YMD (yyyyMMdd) trên mọi INPUT/OUTPUT — PRODUCT={m.ProductId}.");
            }
        }
    }

    private static ValuationAverageResult PeriodEndAverage(
        IReadOnlyList<ValuationMovementInput> ordered,
        ValuationGroupKey key)
    {
        var errors = new List<string>();
        var openQty = SumQty(ordered, "OPEN");
        var openAmt = SumAmt(ordered, "OPEN");
        var inQty = SumQty(ordered, "INPUT");
        var inAmt = SumAmt(ordered, "INPUT");
        var outQty = SumQty(ordered, "OUTPUT");

        var totalQty = openQty + inQty;
        var totalAmt = openAmt + inAmt;
        var avg = totalQty <= 0 ? 0m : totalAmt / totalQty;
        var endQty = totalQty - outQty;
        if (endQty < -ReceiptReconcile.QtyTolerance)
            errors.Add($"Nhóm {key}: tồn cuối âm ({endQty}) — dữ liệu không đủ/không hợp lệ.");

        if (outQty > totalQty + ReceiptReconcile.QtyTolerance)
            errors.Add($"Nhóm {key}: xuất ({outQty}) vượt tồn khả dụng ({totalQty}).");

        var endAmt = Math.Max(0m, endQty * avg);
        var outAmt = Math.Max(0m, outQty * avg);
        var outUnit = outQty > 0 ? outAmt / outQty : 0m;

        return BuildResult(
            GkValuationMethod.PeriodEndAverage, key,
            inQty, inAmt, outQty, outAmt, outUnit, endQty, endAmt, avg, errors);
    }

    private static ValuationAverageResult MovingAverage(
        IReadOnlyList<ValuationMovementInput> ordered,
        ValuationGroupKey key)
    {
        var errors = new List<string>();
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
                if (m.Quantity > currentQty + ReceiptReconcile.QtyTolerance)
                {
                    errors.Add(
                        $"Nhóm {key}: xuất Q={m.Quantity} ngày {m.TxnYmd} vượt tồn {currentQty} — không coi kết quả hợp lệ.");
                    return BuildResult(
                        GkValuationMethod.MovingAverage, key,
                        inQty, inAmt, outQty, outCost, 0, currentQty, currentAmt, currentPrice, errors);
                }

                if (currentQty <= 0 && m.Quantity > 0)
                {
                    errors.Add($"Nhóm {key}: không đủ tồn để tính giá xuất ngày {m.TxnYmd}.");
                    return BuildResult(
                        GkValuationMethod.MovingAverage, key,
                        inQty, inAmt, outQty, outCost, 0, currentQty, currentAmt, currentPrice, errors);
                }

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

        var outUnit = outQty > 0 ? outCost / outQty : 0m;
        var endAvg = currentQty > 0 ? currentAmt / currentQty : 0m;
        return BuildResult(
            GkValuationMethod.MovingAverage, key,
            inQty, inAmt, outQty, outCost, outUnit, currentQty, currentAmt, endAvg, errors);
    }

    private static ValuationAverageResult BuildResult(
        string method,
        ValuationGroupKey key,
        decimal inQty,
        decimal inAmt,
        decimal outQty,
        decimal outAmt,
        decimal outUnit,
        decimal endQty,
        decimal endAmt,
        decimal endUnit,
        List<string> errors) =>
        new()
        {
            MethodCode = method,
            Group = key,
            ProductId = key.ProductId,
            ProductCd = key.ProductCd,
            StoreId = key.StoreId,
            StoreCd = key.StoreCd,
            ChitType = key.ChitType,
            CompanyCd = key.CompanyCd,
            InputQuantity = inQty,
            InputAmount = inAmt,
            OutputQuantity = outQty,
            OutputAmount = AllocationMath.RoundDigit(outAmt, 0),
            OutputAverageUnitCost = outUnit,
            EndingQuantity = endQty,
            EndingAmount = AllocationMath.RoundDigit(endAmt, 0),
            EndingAverageUnitCost = endUnit,
            IsValid = errors.Count == 0,
            Errors = errors
        };

    private static decimal SumQty(IEnumerable<ValuationMovementInput> rows, string flow) =>
        rows.Where(m => string.Equals(m.FlowType, flow, StringComparison.OrdinalIgnoreCase)).Sum(m => m.Quantity);

    private static decimal SumAmt(IEnumerable<ValuationMovementInput> rows, string flow) =>
        rows.Where(m => string.Equals(m.FlowType, flow, StringComparison.OrdinalIgnoreCase)).Sum(m => m.AmountCc);

    private static ValuationMovementInput Clone(ValuationMovementInput m, decimal? amountCc = null) =>
        new()
        {
            ProductId = m.ProductId,
            ProductCd = m.ProductCd,
            ProductName = m.ProductName,
            DepartmentId = m.DepartmentId,
            StoreId = m.StoreId,
            StoreCd = m.StoreCd,
            StoreName = m.StoreName,
            ChitType = m.ChitType,
            InputId = m.InputId,
            InventoryId = m.InventoryId,
            ChitdetailId = m.ChitdetailId,
            TxnYmd = m.TxnYmd,
            FlowType = m.FlowType,
            Quantity = m.Quantity,
            AmountCc = amountCc ?? m.AmountCc,
            IsExpTo154Only = m.IsExpTo154Only,
            DebitAccountCd = m.DebitAccountCd,
            CreditAccountCd = m.CreditAccountCd,
            CompanyCd = m.CompanyCd
        };
}
