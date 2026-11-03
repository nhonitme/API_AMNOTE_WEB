using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Bản ghi nhập kho authoritative để đối chiếu Confirm B5 (INPUT_ID + COMPANY_CD).
/// </summary>
public sealed class InventoryInputValuationRow
{
    public long InputId { get; init; }
    public string CompanyCd { get; init; } = string.Empty;
    public long ProductId { get; init; }
    public string? ProductCd { get; init; }
    public long? StoreId { get; init; }
    public string? StoreCd { get; init; }
    public string? PeriodYm { get; init; }
    public long? DepartmentId { get; init; }
    public decimal Qty { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }
    public long? InventoryId { get; init; }
}

/// <summary>
/// Đối chiếu từng INPUT_ID với dòng nhập kho thực tế — không cập nhật hàng loạt theo PRODUCT_CD.
/// </summary>
public static class ValuationInputIdMatch
{
    public static void AssertMatchesReceipt(
        string companyCd,
        string periodYm,
        ReceiptPricePrepLine receipt,
        InventoryInputValuationRow? inventoryRow)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new ArgumentException("COMPANY_CD bắt buộc.");
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        ArgumentNullException.ThrowIfNull(receipt);

        if (receipt.InputId <= 0)
            throw new InvalidOperationException("INPUT_ID không hợp lệ trong plan Confirm B5.");

        if (inventoryRow == null)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId} không tồn tại trong kho thuộc COMPANY_CD={companyCd.Trim()}.");
        }

        if (inventoryRow.InputId != receipt.InputId)
        {
            throw new InvalidOperationException(
                $"INPUT_ID kho ({inventoryRow.InputId}) ≠ receipt ({receipt.InputId}).");
        }

        if (!string.Equals(
                (inventoryRow.CompanyCd ?? string.Empty).Trim(),
                companyCd.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId} thuộc COMPANY_CD={inventoryRow.CompanyCd}, không phải {companyCd}.");
        }

        if (!string.IsNullOrWhiteSpace(receipt.CompanyCd)
            && !string.Equals(receipt.CompanyCd.Trim(), companyCd.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId} receipt COMPANY_CD={receipt.CompanyCd} ≠ {companyCd}.");
        }

        if (inventoryRow.ProductId != receipt.ProductId)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId}: PRODUCT_ID kho ({inventoryRow.ProductId}) ≠ receipt ({receipt.ProductId}).");
        }

        if (receipt.DepartmentId > 0
            && inventoryRow.DepartmentId is > 0
            && inventoryRow.DepartmentId.Value != receipt.DepartmentId)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId}: DEPARTMENT_ID kho ({inventoryRow.DepartmentId}) ≠ receipt ({receipt.DepartmentId}).");
        }

        if (!string.IsNullOrWhiteSpace(inventoryRow.PeriodYm)
            && !string.Equals(inventoryRow.PeriodYm.Trim(), periodYm.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId}: PERIOD_YM kho ({inventoryRow.PeriodYm}) ≠ kỳ định giá ({periodYm}).");
        }

        if (Math.Abs(inventoryRow.Qty - receipt.Qty) > ReceiptReconcile.QtyTolerance)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={receipt.InputId}: QTY kho ({inventoryRow.Qty}) ≠ receipt ({receipt.Qty}).");
        }
    }

    public static void AssertAllReceipts(
        string companyCd,
        string periodYm,
        IReadOnlyList<ReceiptPricePrepLine> receipts,
        IReadOnlyDictionary<long, InventoryInputValuationRow> inventoryByInputId)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(inventoryByInputId);

        var seen = new HashSet<long>();
        foreach (var r in receipts)
        {
            if (!seen.Add(r.InputId))
                throw new InvalidOperationException($"INPUT_ID trùng trong plan Confirm B5: {r.InputId}.");

            inventoryByInputId.TryGetValue(r.InputId, out var row);
            AssertMatchesReceipt(companyCd, periodYm, r, row);
        }
    }
}
