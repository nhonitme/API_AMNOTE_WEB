using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Đối chiếu phiếu nhập (INPUT_ID) với đối tượng giá thành PRODUCT+DEPT.
/// Không tự phân bổ chênh lệch khi SL hoặc tập đối tượng không khớp.
/// </summary>
public static class ReceiptReconcile
{
    /// <summary>Dung sai làm tròn tiền (RoundDigit 0 → cho phép lệch 1).</summary>
    public const decimal AmountTolerance = 1m;

    /// <summary>Dung sai số lượng (decimal 6).</summary>
    public const decimal QtyTolerance = 0.000001m;

    public sealed class ReconcileResult
    {
        public bool Ok { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        public decimal WipQtySum { get; init; }
        public decimal ReceiptQtySum { get; init; }
        public decimal WipCostSum { get; init; }
        public decimal ReceiptNewAmountSum { get; init; }
    }

    public static ReconcileResult Check(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepLine> receipts)
    {
        var wip = costs.ToList();
        var rec = receipts.ToList();
        var errors = new List<string>();

        var wipKeys = wip.Select(c => (c.ProductId, c.DepartmentId)).ToHashSet();
        foreach (var r in rec)
        {
            if (r.DepartmentId <= 0 && wip.Count(c => c.ProductId == r.ProductId) > 1)
            {
                errors.Add(
                    $"INPUT_ID={r.InputId}: thiếu DEPARTMENT_ID hợp lệ khi PRODUCT={r.ProductId} có nhiều phòng ban — không suy đoán.");
                continue;
            }

            var key = (r.ProductId, r.DepartmentId);
            if (r.DepartmentId > 0 && !wipKeys.Contains(key))
            {
                errors.Add(
                    $"INPUT_ID={r.InputId}: không có đối tượng GT PRODUCT={r.ProductId} DEPT={r.DepartmentId}.");
            }
        }

        // Theo từng đối tượng PRODUCT+DEPT
        var receiptByKey = rec
            .Where(r => r.DepartmentId > 0)
            .GroupBy(r => (r.ProductId, r.DepartmentId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var cost in wip)
        {
            var key = (cost.ProductId, cost.DepartmentId);
            if (!receiptByKey.TryGetValue(key, out var lines))
                continue; // đối tượng chưa gắn phiếu — không tự tạo

            var qtySum = lines.Sum(l => l.Qty);
            var amountSum = AllocationMath.RoundDigit(lines.Sum(l => l.NewAmount), 0);
            var expectedQty = cost.Quantity;
            var expectedAmount = cost.TotalCostPrice;

            if (Math.Abs(qtySum - expectedQty) > QtyTolerance)
            {
                errors.Add(
                    $"PRODUCT={cost.ProductId} DEPT={cost.DepartmentId}: Σ QTY phiếu ({qtySum}) " +
                    $"≠ sản lượng hoàn thành ({expectedQty}) — không tự phân bổ chênh lệch.");
            }

            if (Math.Abs(amountSum - expectedAmount) > AmountTolerance)
            {
                errors.Add(
                    $"PRODUCT={cost.ProductId} DEPT={cost.DepartmentId}: Σ NEW_AMOUNT ({amountSum}) " +
                    $"≠ TOTAL_COST_PRICE ({expectedAmount}) — lệch vượt dung sai làm tròn {AmountTolerance}.");
            }
        }

        // Phiếu gắn DEPT không khớp bất kỳ WIP — đã báo ở trên; phiếu DEPT=0 + đúng 1 WIP product
        foreach (var r in rec.Where(x => x.DepartmentId <= 0))
        {
            var matches = wip.Where(c => c.ProductId == r.ProductId).ToList();
            if (matches.Count == 1)
            {
                // Cho phép đối chiếu gộp theo PRODUCT khi chỉ 1 DEPT
                var cost = matches[0];
                var sibling = rec.Where(x => x.ProductId == r.ProductId).ToList();
                var qtySum = sibling.Sum(l => l.Qty);
                var amountSum = AllocationMath.RoundDigit(sibling.Sum(l => l.NewAmount), 0);
                if (Math.Abs(qtySum - cost.Quantity) > QtyTolerance)
                {
                    errors.Add(
                        $"PRODUCT={r.ProductId}: Σ QTY phiếu ({qtySum}) ≠ QUANTITY ({cost.Quantity}).");
                }

                if (Math.Abs(amountSum - cost.TotalCostPrice) > AmountTolerance)
                {
                    errors.Add(
                        $"PRODUCT={r.ProductId}: Σ NEW_AMOUNT ({amountSum}) ≠ TOTAL_COST_PRICE ({cost.TotalCostPrice}).");
                }
            }
        }

        return new ReconcileResult
        {
            Ok = errors.Count == 0,
            Errors = errors,
            WipQtySum = wip.Sum(c => c.Quantity),
            ReceiptQtySum = rec.Sum(r => r.Qty),
            WipCostSum = AllocationMath.RoundDigit(wip.Sum(c => c.TotalCostPrice), 0),
            ReceiptNewAmountSum = AllocationMath.RoundDigit(rec.Sum(r => r.NewAmount), 0)
        };
    }

    public static void AssertMatches(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepLine> receipts)
    {
        var result = Check(costs, receipts);
        if (!result.Ok)
            throw new InvalidOperationException(result.Errors[0]);
    }
}
