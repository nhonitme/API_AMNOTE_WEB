using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public sealed class InventoryOpeningService : IInventoryOpeningService
    {
        private readonly IInventoryOpeningRepository _repository;
        private readonly ILogger<InventoryOpeningService> _logger;

        public InventoryOpeningService(
            IInventoryOpeningRepository repository,
            ILogger<InventoryOpeningService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public Task<IReadOnlyList<InventoryOpening>> GetListAsync(string companyCd, long? inputId = null)
            => _repository.GetListAsync(companyCd, inputId);

        public Task<InventoryOpening> CreateAsync(string companyCd, string userId, InventoryOpeningRequest request)
            => _repository.CreateAsync(companyCd, userId, request);

        public Task<InventoryOpening> UpdateAsync(string companyCd, string userId, long inputId, InventoryOpeningRequest request)
            => _repository.UpdateAsync(companyCd, userId, inputId, request);

        public Task<int> DeleteAsync(string companyCd, string userId, IReadOnlyList<long> inputIds)
            => _repository.DeleteAsync(companyCd, userId, inputIds);

        public async Task<int> BulkInsertAsync(
            string companyCd,
            string userId,
            IReadOnlyList<InventoryOpeningRequest> records,
            string? databaseName = null)
        {
            _ = databaseName;
            var inserted = 0;
            foreach (var record in records)
            {
                await _repository.CreateAsync(companyCd, userId, record);
                inserted++;
            }

            _logger.LogInformation(
                "Bulk inserted {Count} inventory opening rows for company {CompanyCd}",
                inserted,
                companyCd);
            return inserted;
        }
    }
}
