using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Services
{
    public class InventoryValuationService : IInventoryValuationService
    {
        private readonly IInventoryValuationRepository _repository;
        private readonly ILogger<InventoryValuationService> _logger;

        public InventoryValuationService(
            IInventoryValuationRepository repository,
            ILogger<InventoryValuationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<InventoryValuationCalculateResult> CalculateAsync(
            string companyCd,
            string fromYmd,
            string toYmd,
            string methodCode,
            string? databaseName = null,
            string? productCds = null,
            string? storeCds = null,
            Action<int, int, int, string?>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedMethodCode = InventoryValuationMethod.Normalize(methodCode);
            var (processedGroups, movementCount) = await RunCalculationAsync(
                companyCd,
                fromYmd,
                toYmd,
                databaseName,
                productCds,
                storeCds,
                normalizedMethodCode,
                onProgress,
                cancellationToken);

            _logger.LogInformation(
                "Inventory valuation calculated for company {CompanyCd}, period {FromYmd}-{ToYmd}, method {MethodCode}, groups {GroupCount}, movements {MovementCount}",
                companyCd,
                fromYmd,
                toYmd,
                normalizedMethodCode,
                processedGroups,
                movementCount);

            return new InventoryValuationCalculateResult
            {
                MethodCode = normalizedMethodCode,
                MethodName = InventoryValuationMethod.GetDisplayName(normalizedMethodCode),
                ProcessedGroups = processedGroups,
                MovementCount = movementCount
            };
        }

        private async Task<(int ProcessedGroups, int MovementCount)> RunCalculationAsync(
            string companyCd,
            string fromYmd,
            string toYmd,
            string? databaseName,
            string? productCds,
            string? storeCds,
            string methodCode,
            Action<int, int, int, string?>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new ArgumentException("COMPANY_CD is required");
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("DATABASE_NAME is required");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var movements = (await _repository.GetMovementsAsync(companyCd, fromYmd, toYmd, databaseName, productCds, storeCds))
                .OrderBy(m => m.PRODUCT_CD)
                .ThenBy(m => m.STORE_CD)
                .ThenBy(m => m.TXN_YMD)
                .ThenBy(m => m.CHIT_ID)
                .ThenBy(m => m.CHITDETAIL_ID)
                .ToList();

            onProgress?.Invoke(0, 0, movements.Count, "Đang tính giá xuất kho...");

            var groupList = movements
                .GroupBy(m => new { m.PRODUCT_CD, m.PRODUCT_NAME, m.STORE_CD, m.STORE_NAME, m.CHIT_TYPE })
                .ToList();

            var totalGroups = groupList.Count;
            var processedGroups = 0;

            foreach (var group in groupList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ComputeMethod(group, methodCode);
                processedGroups++;
                onProgress?.Invoke(
                    processedGroups,
                    totalGroups,
                    movements.Count,
                    $"Đang xử lý {processedGroups}/{totalGroups} nhóm...");
            }

            return (processedGroups, movements.Count);
        }

        private static InventoryValuationMethodResult ComputeMethod(
            IEnumerable<InventoryValuationMovement> movements,
            string methodCode)
        {
            return methodCode switch
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
                {
                    continue;
                }

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
                            {
                                continue;
                            }

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
}
