using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IInventoryVoucherRepository
    {
        Task<(IReadOnlyList<InventoryVoucherDto> Items, int TotalRecords)> GetInventoryVouchersPagedAsync(string companyCd, string inputType, string chitType, long? chitId = null, string? searchText = null, string? fromYmd = null, string? toYmd = null, int pageNumber = 1, int pageSize = 20);
        Task<long> SaveInventoryVoucherAsync(string companyCd, string inputType, string userId, InventoryVoucherRequest request, string? databaseName = null);
        Task<int> DeleteInventoryVoucherAsync(string companyCd, string inputType, string chitType, long chitId, string userId);
        Task<bool> InventoryVoucherCdExistsAsync(string companyCd, string inputType, string chitCd, long? excludeChitId = null);
    }
}
