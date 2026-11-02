using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.Inventory;
using Microsoft.Extensions.Logging;
using System;
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
                .ToList();

            onProgress?.Invoke(0, 0, movements.Count, "Đang tính giá xuất kho...");

            // Engine chung — cùng nhóm/công thức với G-K Preview. Kết quả hiện không persist (xem ACCEPTANCE_2C_04).
            var groups = InventoryOutPriceEngine.ComputeByGroups(movements, methodCode);
            var totalGroups = groups.Count;
            var processedGroups = 0;

            foreach (var _ in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedGroups++;
                onProgress?.Invoke(
                    processedGroups,
                    totalGroups,
                    movements.Count,
                    $"Đang xử lý {processedGroups}/{totalGroups} nhóm...");
            }

            return (processedGroups, movements.Count);
        }
    }
}
