using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Chuẩn bị cập nhật giá nhập thành phẩm theo INPUT_ID — không ghi kho (IsApplied=false).
/// </summary>
public static class ReceiptPricePrep
{
    public static IReadOnlyList<ReceiptPricePrepLine> Build(
        IEnumerable<WipCalcResult> costs,
        IEnumerable<ReceiptPricePrepInput> receipts)
    {
        var unitByProduct = costs.ToDictionary(c => c.ProductId, c => c.UnitCost);
        var lines = new List<ReceiptPricePrepLine>();

        foreach (var r in receipts)
        {
            if (r.InputId <= 0)
                throw new ArgumentException($"INPUT_ID không hợp lệ cho PRODUCT_ID={r.ProductId}.");
            if (!unitByProduct.TryGetValue(r.ProductId, out var unit))
                throw new InvalidOperationException(
                    $"Chưa có UNIT_COST cho PRODUCT_ID={r.ProductId} — không chuẩn bị phiếu INPUT_ID={r.InputId}.");

            var newAmount = AllocationMath.RoundDigit(unit * r.Qty, 0);
            lines.Add(new ReceiptPricePrepLine
            {
                ProductId = r.ProductId,
                InputId = r.InputId,
                InventoryId = r.InventoryId,
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

        return lines;
    }
}
