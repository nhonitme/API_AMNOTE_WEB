using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IInventoryOpeningService
    {
        Task<IReadOnlyList<InventoryOpening>> GetListAsync(string companyCd, long? inputId = null);

        Task<InventoryOpening> CreateAsync(string companyCd, string userId, InventoryOpeningRequest request);

        Task<InventoryOpening> UpdateAsync(string companyCd, string userId, long inputId, InventoryOpeningRequest request);

        Task<int> DeleteAsync(string companyCd, string userId, IReadOnlyList<long> inputIds);

        Task<int> BulkInsertAsync(
            string companyCd,
            string userId,
            IReadOnlyList<InventoryOpeningRequest> records,
            string? databaseName = null);
    }
}
