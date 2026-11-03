using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.Inventory;

/// <summary>
/// Engine tính giá xuất kho dùng chung cho menu Kho (<c>/inventory/calc-out-price</c>) và Preview G-K Bước 5.
/// Công thức / nhóm / thứ tự khớp nguyên bản <see cref="InventoryValuationService"/> — không đổi nghiệp vụ.
/// </summary>
public static class InventoryOutPriceEngine
{
    /// <summary>
    /// Khóa nhóm tồn tại: PRODUCT_CD + PRODUCT_NAME + STORE_CD + STORE_NAME + CHIT_TYPE.
    /// </summary>
    public readonly record struct GroupKey(
        string ProductCd,
        string ProductName,
        string StoreCd,
        string StoreName,
        string ChitType);

    public static GroupKey KeyOf(InventoryValuationMovement m) => new(
        m.PRODUCT_CD ?? string.Empty,
        m.PRODUCT_NAME ?? string.Empty,
        m.STORE_CD ?? string.Empty,
        m.STORE_NAME ?? string.Empty,
        m.CHIT_TYPE ?? string.Empty);

    /// <summary>PRODUCT_CD → STORE_CD → TXN_YMD → INVENTORY_ID → CHITDETAIL_ID.</summary>
    public static IReadOnlyList<InventoryValuationMovement> OrderMovements(
        IEnumerable<InventoryValuationMovement> movements) =>
        movements
            .OrderBy(m => m.PRODUCT_CD)
            .ThenBy(m => m.STORE_CD)
            .ThenBy(m => m.TXN_YMD)
            .ThenBy(m => m.INVENTORY_ID)
            .ThenBy(m => m.CHITDETAIL_ID)
            .ToList();

    /// <summary>
    /// Chỉ sắp xếp + tách nhóm — chưa tính giá. Dùng cho progress thực tế theo từng nhóm.
    /// </summary>
    public static IReadOnlyList<IGrouping<GroupKey, InventoryValuationMovement>> BuildOrderedGroups(
        IEnumerable<InventoryValuationMovement> movements) =>
        OrderMovements(movements).GroupBy(KeyOf).ToList();

    /// <summary>
    /// Tính từng nhóm tuần tự: progress/cancel giữa các nhóm; không tính hết rồi giả lập tiến độ.
    /// </summary>
    public static IReadOnlyList<(GroupKey Key, InventoryValuationMethodResult Result)> ComputeByGroups(
        IEnumerable<InventoryValuationMovement> movements,
        string methodCode,
        Action<int, int, GroupKey>? onGroupComputed = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = InventoryValuationMethod.Normalize(methodCode);
        var groups = BuildOrderedGroups(movements);
        var totalGroups = groups.Count;
        var results = new List<(GroupKey Key, InventoryValuationMethodResult Result)>(totalGroups);
        var processed = 0;

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ComputeMethod(group, normalized);
            processed++;
            onGroupComputed?.Invoke(processed, totalGroups, group.Key);
            results.Add((group.Key, result));
        }

        return results;
    }

    public static InventoryValuationMethodResult ComputeMethod(
        IEnumerable<InventoryValuationMovement> movements,
        string methodCode)
    {
        var normalized = InventoryValuationMethod.Normalize(methodCode);
        return normalized switch
        {
            InventoryValuationMethod.PeriodEndAverage => ComputePeriodEndAverage(movements),
            InventoryValuationMethod.MovingAverage => ComputeMovingAverage(movements),
            InventoryValuationMethod.Fifo => ComputeFifo(movements),
            InventoryValuationMethod.Specific => ComputeSpecificIdentification(movements),
            _ => throw new ArgumentException($"Invalid METHOD_CODE: {methodCode}")
        };
    }

    private static InventoryValuationMethodResult ComputePeriodEndAverage(IEnumerable<InventoryValuationMovement> movements)
    {
        var ordered = movements.ToList();
        var openQuantity = ordered.Where(m => string.Equals(m.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase)).Sum(m => m.QUANTITY);
        var openAmount = ordered.Where(m => string.Equals(m.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase)).Sum(m => m.AMOUNT_CC);
        var totalInputQuantity = ordered.Where(m => string.Equals(m.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase)).Sum(m => m.QUANTITY);
        var totalInputAmount = ordered.Where(m => string.Equals(m.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase)).Sum(m => m.AMOUNT_CC);
        var totalOutputQuantity = ordered.Where(m => string.Equals(m.FLOW_TYPE, "OUTPUT", StringComparison.OrdinalIgnoreCase)).Sum(m => m.QUANTITY);

        var totalQuantity = openQuantity + totalInputQuantity;
        var totalAmount = openAmount + totalInputAmount;
        var averagePrice = totalQuantity <= 0 ? 0m : totalAmount / totalQuantity;
        var endingQuantity = totalQuantity - totalOutputQuantity;
        var endingAmount = Math.Max(0m, endingQuantity * averagePrice);
        var outputAmount = Math.Max(0m, totalOutputQuantity * averagePrice);

        return new InventoryValuationMethodResult
        {
            MethodCode = InventoryValuationMethod.PeriodEndAverage,
            MethodName = "Bình quân cuối kỳ",
            InputQuantity = totalInputQuantity,
            InputAmount = totalInputAmount,
            OutputQuantity = totalOutputQuantity,
            OutputAmount = outputAmount,
            EndingQuantity = endingQuantity,
            EndingAmount = endingAmount,
            EndingAverageUnitCost = averagePrice
        };
    }

    private static InventoryValuationMethodResult ComputeMovingAverage(IEnumerable<InventoryValuationMovement> movements)
    {
        var ordered = movements.ToList();
        var currentQuantity = ordered.Where(m => string.Equals(m.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase)).Sum(m => m.QUANTITY);
        var currentAmount = ordered.Where(m => string.Equals(m.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase)).Sum(m => m.AMOUNT_CC);
        var currentPrice = currentQuantity > 0 ? currentAmount / currentQuantity : 0m;
        var totalInputQuantity = 0m;
        var totalInputAmount = 0m;
        var totalOutputQuantity = 0m;
        var totalOutputCost = 0m;

        foreach (var movement in ordered)
        {
            if (string.Equals(movement.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(movement.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase))
            {
                var inputQuantity = movement.QUANTITY;
                var inputAmount = movement.AMOUNT_CC;
                currentQuantity += inputQuantity;
                currentAmount += inputAmount;
                currentPrice = currentQuantity > 0 ? currentAmount / currentQuantity : 0m;
                totalInputQuantity += inputQuantity;
                totalInputAmount += inputAmount;
            }
            else if (string.Equals(movement.FLOW_TYPE, "OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                var outputQuantity = movement.QUANTITY;
                var cost = outputQuantity * currentPrice;
                totalOutputQuantity += outputQuantity;
                totalOutputCost += cost;
                currentQuantity -= outputQuantity;
                currentAmount -= cost;
                if (currentQuantity <= 0m)
                {
                    currentQuantity = 0m;
                    currentAmount = 0m;
                    currentPrice = 0m;
                }
                else
                {
                    currentPrice = currentAmount / currentQuantity;
                }
            }
        }

        var endingAverage = currentQuantity > 0 ? currentAmount / currentQuantity : 0m;
        return new InventoryValuationMethodResult
        {
            MethodCode = InventoryValuationMethod.MovingAverage,
            MethodName = "Bình quân tức thời",
            InputQuantity = totalInputQuantity,
            InputAmount = totalInputAmount,
            OutputQuantity = totalOutputQuantity,
            OutputAmount = totalOutputCost,
            EndingQuantity = currentQuantity,
            EndingAmount = currentAmount,
            EndingAverageUnitCost = endingAverage
        };
    }

    private static InventoryValuationMethodResult ComputeFifo(IEnumerable<InventoryValuationMovement> movements)
    {
        var ordered = movements.ToList();
        var queue = new Queue<(decimal Quantity, decimal UnitCost)>();
        var totalInputQuantity = 0m;
        var totalInputAmount = 0m;
        var totalOutputQuantity = 0m;
        var totalOutputCost = 0m;

        foreach (var movement in ordered)
        {
            if (string.Equals(movement.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(movement.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase))
            {
                var layerQuantity = movement.QUANTITY;
                var layerAmount = movement.AMOUNT_CC;
                var unitCost = layerQuantity > 0 ? layerAmount / layerQuantity : 0m;
                queue.Enqueue((layerQuantity, unitCost));
                if (string.Equals(movement.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase))
                {
                    totalInputQuantity += layerQuantity;
                    totalInputAmount += layerAmount;
                }
            }
            else if (string.Equals(movement.FLOW_TYPE, "OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                var remaining = movement.QUANTITY;
                totalOutputQuantity += movement.QUANTITY;
                while (remaining > 0m && queue.Count > 0)
                {
                    var lot = queue.Dequeue();
                    var consume = Math.Min(lot.Quantity, remaining);
                    totalOutputCost += consume * lot.UnitCost;
                    remaining -= consume;
                    if (lot.Quantity > consume)
                    {
                        queue.Enqueue((lot.Quantity - consume, lot.UnitCost));
                    }
                }
            }
        }

        var endingQuantity = queue.Sum(x => x.Quantity);
        var endingAmount = queue.Sum(x => x.Quantity * x.UnitCost);
        var endingAverage = endingQuantity > 0 ? endingAmount / endingQuantity : 0m;

        return new InventoryValuationMethodResult
        {
            MethodCode = InventoryValuationMethod.Fifo,
            MethodName = "Nhập trước xuất trước",
            InputQuantity = totalInputQuantity,
            InputAmount = totalInputAmount,
            OutputQuantity = totalOutputQuantity,
            OutputAmount = totalOutputCost,
            EndingQuantity = endingQuantity,
            EndingAmount = endingAmount,
            EndingAverageUnitCost = endingAverage
        };
    }

    private static InventoryValuationMethodResult ComputeSpecificIdentification(IEnumerable<InventoryValuationMovement> movements)
    {
        var ordered = movements.ToList();
        var layers = new List<(decimal Quantity, decimal UnitCost, long? DetailId)>();
        var totalInputQuantity = 0m;
        var totalInputAmount = 0m;
        var totalOutputQuantity = 0m;
        var totalOutputCost = 0m;

        foreach (var movement in ordered)
        {
            if (string.Equals(movement.FLOW_TYPE, "OPEN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(movement.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase))
            {
                var layerQuantity = movement.QUANTITY;
                var layerAmount = movement.AMOUNT_CC;
                var unitCost = layerQuantity > 0 ? layerAmount / layerQuantity : 0m;
                layers.Add((layerQuantity, unitCost, movement.CHITDETAIL_ID));
                if (string.Equals(movement.FLOW_TYPE, "INPUT", StringComparison.OrdinalIgnoreCase))
                {
                    totalInputQuantity += layerQuantity;
                    totalInputAmount += layerAmount;
                }
            }
            else if (string.Equals(movement.FLOW_TYPE, "OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                totalOutputQuantity += movement.QUANTITY;
                var remaining = movement.QUANTITY;
                var outputCost = 0m;
                var detailId = movement.CHITDETAIL_ID;

                if (detailId.HasValue)
                {
                    for (var index = 0; index < layers.Count && remaining > 0m; index++)
                    {
                        var layer = layers[index];
                        if (layer.DetailId != detailId)
                            continue;

                        var allocate = Math.Min(layer.Quantity, remaining);
                        outputCost += allocate * layer.UnitCost;
                        remaining -= allocate;
                        var remainingLayer = layer.Quantity - allocate;
                        layers[index] = (remainingLayer, layer.UnitCost, layer.DetailId);
                    }

                    layers.RemoveAll(layer => layer.Quantity <= 0m);
                }

                if (remaining > 0m && outputCost == 0m)
                {
                    outputCost = movement.AMOUNT_CC > 0m ? movement.AMOUNT_CC : movement.QUANTITY * movement.UNIT_PRICE_CC;
                }

                totalOutputCost += outputCost;
            }
        }

        var endingQuantity = layers.Sum(x => x.Quantity);
        var endingAmount = layers.Sum(x => x.Quantity * x.UnitCost);
        var endingAverage = endingQuantity > 0 ? endingAmount / endingQuantity : 0m;

        return new InventoryValuationMethodResult
        {
            MethodCode = InventoryValuationMethod.Specific,
            MethodName = "Giá đích danh",
            InputQuantity = totalInputQuantity,
            InputAmount = totalInputAmount,
            OutputQuantity = totalOutputQuantity,
            OutputAmount = totalOutputCost,
            EndingQuantity = endingQuantity,
            EndingAmount = endingAmount,
            EndingAverageUnitCost = endingAverage
        };
    }
}
