using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IInventoryLinkRepository
    {
        Task<IReadOnlyList<InventoryInput>> GetInventoryInputsByChitDetailIdsAsync(string companyCd, IEnumerable<long> chitDetailIds, string? databaseName = null);
        Task<IReadOnlyList<InventoryOutput>> GetInventoryOutputsByChitDetailIdsAsync(string companyCd, IEnumerable<long> chitDetailIds, string? databaseName = null);
        Task<IReadOnlyList<InventoryInput>> GetInventoryInputsByChitIdsAsync(string companyCd, IEnumerable<long> chitIds, string? databaseName = null);
        Task<IReadOnlyList<InventoryOutput>> GetInventoryOutputsByChitIdsAsync(string companyCd, IEnumerable<long> chitIds, string? databaseName = null);
        Task<ChitInventoryLinkStatusDto?> GetInventoryLinkStatusAsync(string companyCd, string sourceChitType, long sourceChitId);
        Task<IReadOnlyList<ChitInventoryLinkStatusDto>> GetInventoryLinkStatusesAsync(string companyCd, string sourceChitType, IEnumerable<long> sourceChitIds);
        Task<ChitInventorySourceVoucherDto?> GetInventorySourceVoucherByDetailIdAsync(string companyCd, long chitDetailId);
    }
}
