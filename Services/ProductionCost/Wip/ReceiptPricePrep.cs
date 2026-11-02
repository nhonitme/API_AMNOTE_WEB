using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Chuẩn bị cập nhật giá nhập thành phẩm theo INPUT_ID + DEPARTMENT_ID — không ghi kho (IsApplied=false).
/// </summary>
public static class ReceiptPricePrep
{
    public sealed class PrepResult
    {
        public IReadOnlyList<ReceiptPricePrepLine> Lines { get; init; } = Array.Empty<ReceiptPricePrepLine>();
        public decimal NewAmountSum { get; init; }
        public decimal OldAmountSum { get; init; }
        public decimal DeltaSum { get; init; }
    }

    public static IReadOnlyList<ReceiptPricePrepLine> Build(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepInput> receipts,
        string? companyCd = null) =>
        BuildDetailed(costs, receipts, companyCd).Lines;

    public static PrepResult BuildDetailed(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepInput> receipts,
        string? companyCd = null)
    {
        var costList = costs.ToList();
        var receiptList = receipts.ToList();

        var inputIds = new HashSet<long>();
        foreach (var r in receiptList)
        {
            if (r.InputId <= 0)
                throw new ArgumentException($"INPUT_ID không hợp lệ cho PRODUCT_ID={r.ProductId}.");
            if (r.Qty <= 0)
                throw new ArgumentException(
                    $"Quantity phiếu INPUT_ID={r.InputId} phải > 0 (PRODUCT_ID={r.ProductId}).");
            if (!inputIds.Add(r.InputId))
                throw new InvalidOperationException($"INPUT_ID trùng trong lần chuẩn bị giá: {r.InputId}.");
        }

        var lines = new List<ReceiptPricePrepLine>();
        foreach (var r in receiptList)
        {
            var unit = ResolveUnitCost(costList, r.ProductId, r.DepartmentId, r.InputId);
            var newAmount = AllocationMath.RoundDigit(unit * r.Qty, 0);
            // Sai lệch làm tròn: unit*qty vs round — ghi nhận trên NewAmount đã round
            lines.Add(new ReceiptPricePrepLine
            {
                ProductId = r.ProductId,
                DepartmentId = r.DepartmentId,
                InputId = r.InputId,
                InventoryId = r.InventoryId,
                CompanyCd = companyCd ?? string.Empty,
                ProductCd = r.ProductCd,
                InventoryNo = r.InventoryNo,
                Qty = r.Qty,
                OldUnitPrice = r.OldUnitPrice,
                NewUnitPrice = unit,
                OldAmount = r.OldAmount,
                NewAmount = newAmount,
                DeltaAmount = AllocationMath.RoundDigit(newAmount - r.OldAmount, 0),
                IsApplied = false
            });
        }

        var newSum = AllocationMath.RoundDigit(lines.Sum(l => l.NewAmount), 0);
        var oldSum = AllocationMath.RoundDigit(lines.Sum(l => l.OldAmount), 0);
        var deltaSum = AllocationMath.RoundDigit(lines.Sum(l => l.DeltaAmount), 0);
        if (Math.Abs(deltaSum - (newSum - oldSum)) > 1m)
        {
            throw new InvalidOperationException(
                $"Tổng DeltaAmount ({deltaSum}) lệch New−Old ({newSum - oldSum}) sau làm tròn.");
        }

        return new PrepResult
        {
            Lines = lines,
            NewAmountSum = newSum,
            OldAmountSum = oldSum,
            DeltaSum = deltaSum
        };
    }

    private static decimal ResolveUnitCost(
        IReadOnlyList<WipCalcResult> costs,
        long productId,
        long departmentId,
        long inputId)
    {
        var forProduct = costs.Where(c => c.ProductId == productId).ToList();
        if (forProduct.Count == 0)
        {
            throw new InvalidOperationException(
                $"Chưa có UNIT_COST cho PRODUCT_ID={productId} — không chuẩn bị phiếu INPUT_ID={inputId}.");
        }

        if (departmentId > 0)
        {
            var exact = forProduct.Where(c => c.DepartmentId == departmentId).ToList();
            if (exact.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Không có giá thành PRODUCT={productId} DEPT={departmentId} cho INPUT_ID={inputId}.");
            }

            if (exact.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Nhiều dòng giá thành PRODUCT={productId} DEPT={departmentId} — không chọn đơn giá tùy ý.");
            }

            return exact[0].UnitCost;
        }

        // Receipt chưa gắn DEPT: chỉ hợp lệ khi đúng 1 dòng giá thành cho sản phẩm
        if (forProduct.Count > 1)
        {
            throw new InvalidOperationException(
                $"PRODUCT_ID={productId} có nhiều giá thành theo phòng ban — " +
                $"phải chỉ định DEPARTMENT_ID cho INPUT_ID={inputId}.");
        }

        return forProduct[0].UnitCost;
    }
}
