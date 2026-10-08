using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IFixedAssetRepository
    {
        Task<IReadOnlyList<FixedAssetListItemDto>> GetListAsync(string companyCd, string? status, string? accCd);

        Task<FixedAssetDetailResponse?> GetByIdAsync(string companyCd, long assetId);

        Task<long> CreateAsync(FixedAssetSaveRequest request, string userId);

        Task UpdateAsync(long assetId, FixedAssetSaveRequest request, string userId);

        Task DeleteAsync(string companyCd, long assetId, string userId);
    }
}
