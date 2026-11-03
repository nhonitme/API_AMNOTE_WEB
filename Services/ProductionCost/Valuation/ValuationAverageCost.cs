using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.Inventory;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Adapter G-K Bước 5 → <see cref="InventoryOutPriceEngine"/> (cùng menu Kho / calc-out-price).
/// Không phải engine định giá độc lập — nhóm/công thức/thứ tự do InventoryOutPriceEngine quyết định.
/// </summary>
public static class ValuationAverageCost
{
    /// <summary>Khóa nhóm = InventoryValuation: PRODUCT_CD + PRODUCT_NAME + STORE_CD + STORE_NAME + CHIT_TYPE.</summary>
    public static ValuationGroupKey GroupKeyOf(ValuationMovementInput m) => new()
    {
        CompanyCd = (m.CompanyCd ?? string.Empty).Trim(),
        ProductId = m.ProductId,
        ProductCd = (m.ProductCd ?? string.Empty).Trim(),
        ProductName = (m.ProductName ?? string.Empty).Trim(),
        StoreId = m.StoreId ?? 0,
        StoreCd = (m.StoreCd ?? string.Empty).Trim(),
        StoreName = (m.StoreName ?? string.Empty).Trim(),
        ChitType = (m.ChitType ?? string.Empty).Trim()
    };

    public static IReadOnlyList<ValuationMovementInput> OrderLikeInventoryEngine(
        IEnumerable<ValuationMovementInput> movements)
    {
        // Cùng thứ tự InventoryOutPriceEngine; ThenBy index giữ ổn định khi trùng khóa.
        return movements
            .Select((m, index) => (m, index))
            .OrderBy(x => x.m.ProductCd ?? string.Empty)
            .ThenBy(x => x.m.StoreCd ?? string.Empty)
            .ThenBy(x => x.m.TxnYmd ?? string.Empty)
            .ThenBy(x => x.m.InventoryId ?? 0)
            .ThenBy(x => x.m.ChitdetailId ?? 0)
            .ThenBy(x => x.index)
            .Select(x => x.m)
            .ToList();
    }

    public static IReadOnlyList<ValuationAverageResult> ComputeByGroup(
        IEnumerable<ValuationMovementInput> movements,
        string methodCode,
        string? companyCd = null)
    {
        var method = InventoryValuationMethod.Normalize(
            string.IsNullOrWhiteSpace(methodCode) ? GkValuationMethod.PeriodEndAverage : methodCode);

        var list = movements.ToList();
        Cogs632PreviewRules.AssertMovementsSafeForValuation(list);
        AssertCompanyScope(list, companyCd);

        var inventoryMoves = list.Select(ToInventoryMovement).ToList();
        var computed = InventoryOutPriceEngine.ComputeByGroups(inventoryMoves, method);

        // Map metadata từ nguồn theo khóa Kho — không lấy ID dòng đầu khi xung đột
        var byInventoryKey = list
            .GroupBy(m => InventoryOutPriceEngine.KeyOf(ToInventoryMovement(m)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<ValuationAverageResult>();
        foreach (var (key, engineResult) in computed)
        {
            byInventoryKey.TryGetValue(key, out var rows);
            rows ??= new List<ValuationMovementInput>();
            var meta = ResolveGroupMetadata(key, rows, companyCd);
            var outUnit = engineResult.OutputQuantity > 0
                ? engineResult.OutputAmount / engineResult.OutputQuantity
                : 0m;

            results.Add(new ValuationAverageResult
            {
                MethodCode = engineResult.MethodCode,
                Group = new ValuationGroupKey
                {
                    CompanyCd = meta.CompanyCd,
                    ProductId = meta.ProductId,
                    ProductCd = key.ProductCd,
                    ProductName = key.ProductName,
                    StoreId = meta.StoreId,
                    StoreCd = key.StoreCd,
                    StoreName = key.StoreName,
                    ChitType = key.ChitType
                },
                ProductId = meta.ProductId,
                ProductCd = key.ProductCd,
                StoreId = meta.StoreId,
                StoreCd = key.StoreCd,
                ChitType = key.ChitType,
                CompanyCd = meta.CompanyCd,
                InputQuantity = engineResult.InputQuantity,
                InputAmount = engineResult.InputAmount,
                OutputQuantity = engineResult.OutputQuantity,
                OutputAmount = engineResult.OutputAmount,
                OutputAverageUnitCost = outUnit,
                EndingQuantity = engineResult.EndingQuantity,
                EndingAmount = engineResult.EndingAmount,
                EndingAverageUnitCost = engineResult.EndingAverageUnitCost,
                IsValid = true,
                Errors = Array.Empty<string>()
            });
        }

        return results;
    }

    /// <summary>Tương thích gọi đơn nhóm — ném lỗi nếu nhiều nhóm.</summary>
    public static ValuationAverageResult Compute(
        IEnumerable<ValuationMovementInput> movements,
        string methodCode,
        string? companyCd = null)
    {
        var all = ComputeByGroup(movements, methodCode, companyCd);
        if (all.Count == 0)
            throw new InvalidOperationException("Không có nhóm định giá để tính.");
        if (all.Count > 1)
        {
            throw new InvalidOperationException(
                $"Có {all.Count} nhóm định giá — dùng ComputeByGroup (khóa PRODUCT_CD+STORE_CD+CHIT_TYPE).");
        }

        return all[0];
    }

    /// <summary>
    /// Áp giá nhập mới theo INPUT_ID — lớp G-K (không thuộc engine Kho).
    /// Chỉ INPUT cùng COMPANY_CD + PRODUCT_ID + qty khớp; IsApplied luôn false ở đây.
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

    /// <summary>Không trộn nhiều COMPANY_CD; khớp request khi có.</summary>
    internal static void AssertCompanyScope(
        IReadOnlyList<ValuationMovementInput> movements,
        string? companyCd)
    {
        var present = movements
            .Select(m => (m.CompanyCd ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (present.Count > 1)
        {
            throw new InvalidOperationException(
                $"Không trộn movement nhiều COMPANY_CD trong một lần định giá: {string.Join(", ", present)}.");
        }

        if (string.IsNullOrWhiteSpace(companyCd))
            return;

        var expected = companyCd.Trim();
        foreach (var m in movements)
        {
            var cd = (m.CompanyCd ?? string.Empty).Trim();
            if (cd.Length > 0 && !string.Equals(cd, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Movement COMPANY_CD={cd} không khớp request COMPANY_CD={expected}.");
            }
        }

        if (present.Count == 1 &&
            !string.Equals(present[0], expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"COMPANY_CD movements ({present[0]}) ≠ request ({expected}).");
        }
    }

    /// <summary>
    /// PRODUCT_ID / STORE_ID trong cùng nhóm Kho phải nhất quán — không chọn ID dòng đầu khi xung đột.
    /// </summary>
    internal static (long ProductId, long StoreId, string CompanyCd) ResolveGroupMetadata(
        InventoryOutPriceEngine.GroupKey key,
        IReadOnlyList<ValuationMovementInput> rows,
        string? requestCompanyCd)
    {
        var productIds = rows.Select(r => r.ProductId).Where(id => id > 0).Distinct().ToList();
        if (productIds.Count > 1)
        {
            throw new InvalidOperationException(
                $"Nhóm Kho PRODUCT_CD={key.ProductCd} STORE_CD={key.StoreCd} CHIT={key.ChitType}: " +
                $"PRODUCT_ID xung đột ({string.Join(", ", productIds)}) — không lấy ID dòng đầu.");
        }

        var storeIds = rows
            .Select(r => r.StoreId ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (storeIds.Count > 1)
        {
            throw new InvalidOperationException(
                $"Nhóm Kho PRODUCT_CD={key.ProductCd} STORE_CD={key.StoreCd} CHIT={key.ChitType}: " +
                $"STORE_ID xung đột ({string.Join(", ", storeIds)}) — không lấy ID dòng đầu.");
        }

        var companies = rows
            .Select(r => (r.CompanyCd ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (companies.Count > 1)
        {
            throw new InvalidOperationException(
                $"Nhóm Kho PRODUCT_CD={key.ProductCd}: trộn COMPANY_CD ({string.Join(", ", companies)}).");
        }

        var company = companies.Count == 1
            ? companies[0]
            : (requestCompanyCd ?? string.Empty).Trim();

        return (productIds.FirstOrDefault(), storeIds.FirstOrDefault(), company);
    }

    private static InventoryValuationMovement ToInventoryMovement(ValuationMovementInput m) =>
        new()
        {
            FLOW_TYPE = m.FlowType,
            COMPANY_CD = m.CompanyCd ?? string.Empty,
            INVENTORY_ID = m.InventoryId,
            CHITDETAIL_ID = m.ChitdetailId,
            CHIT_TYPE = m.ChitType ?? string.Empty,
            PRODUCT_CD = m.ProductCd ?? string.Empty,
            PRODUCT_NAME = m.ProductName ?? string.Empty,
            STORE_CD = m.StoreCd ?? string.Empty,
            STORE_NAME = m.StoreName ?? string.Empty,
            TXN_YMD = m.TxnYmd,
            QUANTITY = m.Quantity,
            UNIT_PRICE_CC = m.Quantity != 0 ? m.AmountCc / m.Quantity : 0m,
            AMOUNT_CC = m.AmountCc
        };

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
