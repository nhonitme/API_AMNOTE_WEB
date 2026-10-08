using API_AMNOTE_WEB.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICompanySignatureInfoRepository
    {
        Task<IEnumerable<CompanySignatureInfo>> QueryCompanySignatureInfosAsync(
            string companyCd,
            long? id = null,
            string? signCode = null,
            string? isActive = null);

        Task<CompanySignatureInfo?> SetCompanySignatureInfoAsync(string companyCd, string userId, CompanySignatureInfoRequest request);

        Task<int> DeleteCompanySignatureInfoAsync(string companyCd, string userId, List<long> signatureIds);
    }
}
