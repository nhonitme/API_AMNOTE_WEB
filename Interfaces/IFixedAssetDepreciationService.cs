using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IFixedAssetDepreciationService
    {
        FixedAssetDepreciationPreviewResponse Preview(FixedAssetDepreciationPreviewRequest request);
    }
}
