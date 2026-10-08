using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IInventoryVoucherWriteService
    {
        Task<long> CreateAsync(
            string companyCd,
            string inputType,
            string chitType,
            string userId,
            InventoryVoucherRequest request,
            InventoryVoucherCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null);

        Task<long> UpdateAsync(
            string companyCd,
            string inputType,
            string chitType,
            string userId,
            InventoryVoucherDto existing,
            InventoryVoucherRequest request,
            InventoryVoucherCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null);
    }

    public sealed class InventoryVoucherCodeMaps
    {
        public IReadOnlyDictionary<string, long>? ProductIdsByCode { get; init; }
        public IReadOnlyDictionary<string, long>? StoreIdsByCode { get; init; }
        public IReadOnlyDictionary<string, long>? UnitIdsByCode { get; init; }
    }
}
