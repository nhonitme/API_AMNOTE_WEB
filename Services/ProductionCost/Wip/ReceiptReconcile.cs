using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>Chế độ đối chiếu phiếu nhập vs WIP.</summary>
public enum ReceiptReconcileMode
{
    /// <summary>Cho phép thiếu phiếu — trả Warnings; vẫn lỗi khi QTY/AMOUNT lệch trên phiếu đã có.</summary>
    Preview = 0,
    /// <summary>Đủ phiếu cho mọi đối tượng bắt buộc nhập kho trước khi apply giá.</summary>
    ReadyToApply = 1
}

/// <summary>
/// Đối chiếu phiếu nhập (INPUT_ID) với đối tượng giá thành PRODUCT+DEPT.
/// Không tự phân bổ chênh lệch; không suy đoán DEPT.
/// </summary>
public static class ReceiptReconcile
{
    public const decimal AmountTolerance = 1m;
    public const decimal QtyTolerance = 0.000001m;

    public sealed class ReconcileResult
    {
        public bool Ok { get; init; }
        public bool ReadyToApply { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<(long ProductId, long DepartmentId)> MissingRequiredReceipts { get; init; }
            = Array.Empty<(long, long)>();
        public decimal WipQtySum { get; init; }
        public decimal ReceiptQtySum { get; init; }
        public decimal WipCostSum { get; init; }
        public decimal ReceiptNewAmountSum { get; init; }
    }

    /// <summary>Đối tượng QUANTITY &gt; 0 bắt buộc có nhập TP; QUANTITY = 0 → không bắt buộc.</summary>
    public static bool RequiresFinishedGoodsReceipt(WipCalcResult cost) => cost.Quantity > 0;

    public static ReconcileResult Check(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepLine> receipts,
        ReceiptReconcileMode mode = ReceiptReconcileMode.Preview)
    {
        var wip = costs.ToList();
        var rec = receipts.ToList();
        var errors = new List<string>();
        var warnings = new List<string>();
        var missing = new List<(long ProductId, long DepartmentId)>();

        var wipKeys = wip.Select(c => (c.ProductId, c.DepartmentId)).ToHashSet();
        foreach (var r in rec)
        {
            if (r.DepartmentId <= 0 && wip.Count(c => c.ProductId == r.ProductId) > 1)
            {
                errors.Add(
                    $"INPUT_ID={r.InputId}: thiếu DEPARTMENT_ID hợp lệ khi PRODUCT={r.ProductId} có nhiều phòng ban — không suy đoán.");
                continue;
            }

            if (r.DepartmentId > 0 && !wipKeys.Contains((r.ProductId, r.DepartmentId)))
            {
                errors.Add(
                    $"INPUT_ID={r.InputId}: không có đối tượng GT PRODUCT={r.ProductId} DEPT={r.DepartmentId}.");
            }
        }

        var receiptByKey = rec
            .Where(r => r.DepartmentId > 0)
            .GroupBy(r => (r.ProductId, r.DepartmentId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var cost in wip)
        {
            var key = (cost.ProductId, cost.DepartmentId);
            var requires = RequiresFinishedGoodsReceipt(cost);
            receiptByKey.TryGetValue(key, out var lines);
            lines ??= new List<ReceiptPricePrepLine>();

            // DEPT=0 receipts khi chỉ 1 WIP product
            if (lines.Count == 0)
            {
                var zeroDept = rec.Where(r => r.ProductId == cost.ProductId && r.DepartmentId <= 0).ToList();
                if (zeroDept.Count > 0 && wip.Count(c => c.ProductId == cost.ProductId) == 1)
                    lines = zeroDept;
            }

            if (lines.Count == 0)
            {
                if (!requires)
                {
                    warnings.Add(
                        $"PRODUCT={cost.ProductId} DEPT={cost.DepartmentId}: QUANTITY=0 — không bắt buộc phiếu nhập TP.");
                    continue;
                }

                missing.Add(key);
                var msg =
                    $"PRODUCT={cost.ProductId} DEPT={cost.DepartmentId}: thiếu INPUT_ID cho đối tượng bắt buộc nhập kho.";
                if (mode == ReceiptReconcileMode.ReadyToApply)
                    errors.Add(msg);
                else
                    warnings.Add(msg);
                continue;
            }

            var qtySum = lines.Sum(l => l.Qty);
            var amountSum = AllocationMath.RoundDigit(lines.Sum(l => l.NewAmount), 0);

            if (Math.Abs(qtySum - cost.Quantity) > QtyTolerance)
            {
                errors.Add(
                    $"PRODUCT={cost.ProductId} DEPT={cost.DepartmentId}: Σ QTY phiếu ({qtySum}) " +
                    $"≠ sản lượng hoàn thành ({cost.Quantity}) — không tự phân bổ chênh lệch.");
            }

            if (Math.Abs(amountSum - cost.TotalCostPrice) > AmountTolerance)
            {
                errors.Add(
                    $"PRODUCT={cost.ProductId} DEPT={cost.DepartmentId}: Σ NEW_AMOUNT ({amountSum}) " +
                    $"≠ TOTAL_COST_PRICE ({cost.TotalCostPrice}) — lệch vượt dung sai {AmountTolerance}.");
            }
        }

        var ready = errors.Count == 0 && missing.Count == 0;
        return new ReconcileResult
        {
            Ok = errors.Count == 0,
            ReadyToApply = ready,
            Errors = errors,
            Warnings = warnings,
            MissingRequiredReceipts = missing,
            WipQtySum = wip.Sum(c => c.Quantity),
            ReceiptQtySum = rec.Sum(r => r.Qty),
            WipCostSum = AllocationMath.RoundDigit(wip.Sum(c => c.TotalCostPrice), 0),
            ReceiptNewAmountSum = AllocationMath.RoundDigit(rec.Sum(r => r.NewAmount), 0)
        };
    }

    public static void AssertMatches(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepLine> receipts,
        ReceiptReconcileMode mode = ReceiptReconcileMode.ReadyToApply)
    {
        var result = Check(costs, receipts, mode);
        if (mode == ReceiptReconcileMode.ReadyToApply && !result.ReadyToApply)
            throw new InvalidOperationException(
                result.Errors.FirstOrDefault()
                ?? "Chưa đủ phiếu nhập để ReadyToApply.");
        if (!result.Ok)
            throw new InvalidOperationException(result.Errors[0]);
    }
}
