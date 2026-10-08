using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IChitInfoRepository
    {
        Task<IEnumerable<ChitInfo>> GetChitInfosAsync(string companyCd, string inputType, string? chitType = null, long? chitId = null, string? searchText = null, string? fromYmd = null, string? toYmd = null, string? databaseName = null);
        Task<(IReadOnlyList<ChitInfo> Items, int TotalRecords)> GetChitInfosPagedAsync(string companyCd, string inputType, string? chitType = null, long? chitId = null, string? searchText = null, string? fromYmd = null, string? toYmd = null, int pageNumber = 1, int pageSize = 20, string? databaseName = null);
        Task<(IReadOnlyList<ChitInfo> Items, int TotalRecords)> GetChitInfosPagedByInputTypesAsync(string companyCd, IReadOnlyList<string> inputTypes, string? chitType = null, long? chitId = null, string? searchText = null, string? fromYmd = null, string? toYmd = null, int pageNumber = 1, int pageSize = 20, string? databaseName = null);
        Task<IReadOnlyList<ChitDetail>> GetChitDetailsByChitIdsAsync(string companyCd, string inputType, IEnumerable<long> chitIds, string? databaseName = null);
        Task<long> SaveChitInfoAsync(string companyCd, string inputType, string userId, ChitInfoRequest request, string? databaseName = null);
        Task<int> DeleteChitInfoAsync(string companyCd, string inputType, long chitId, string userId);
        Task<bool> ChitCdExistsAsync(string companyCd, string inputType, string chitCd, long? excludeChitId = null);
    }
}
