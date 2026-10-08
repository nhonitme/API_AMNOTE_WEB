using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IInventoryOpeningRepository
    {
        Task<IReadOnlyList<InventoryOpening>> GetListAsync(string companyCd, long? inputId = null);

        Task<InventoryOpening?> GetByIdAsync(string companyCd, long inputId);

        Task<bool> ProductStoreExistsAsync(
            string companyCd,
            long productId,
            long storeId,
            long? excludeInputId = null);

        Task<InventoryOpening> CreateAsync(string companyCd, string userId, InventoryOpeningRequest request);

        Task<InventoryOpening> UpdateAsync(string companyCd, string userId, long inputId, InventoryOpeningRequest request);

        Task<int> DeleteAsync(string companyCd, string userId, IReadOnlyList<long> inputIds);
    }
}
