using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICompanySignatureInfoService
    {
        Task<IEnumerable<CompanySignatureInfo>> GetCompanySignatureInfosAsync(
            string companyCd,
            long? id = null,
            string? signCode = null,
            string? isActive = null);

        Task<CompanySignatureInfo?> SetCompanySignatureInfoAsync(
            string companyCd,
            string userId,
            CompanySignatureInfoRequest request);

        Task<int> DeleteCompanySignatureInfoAsync(string companyCd, string userId, List<long> signatureIds);

        Task ClearCacheAsync(string companyCd);
    }
}
