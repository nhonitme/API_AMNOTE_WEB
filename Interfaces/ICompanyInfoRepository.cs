using API_AMNOTE_WEB.Models;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICompanyInfoRepository
    {
        Task<CompanyInfo?> GetCompanyInfoAsync(string companyCd);
        Task<int> UpsertCompanyInfoAsync(string companyCd, CompanyInfoRequest request, CompanyInfo? existing = null);
    }
}
