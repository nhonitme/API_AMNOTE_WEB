using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ILoginRepository
    {
        Task<LoginInfo?> LoginAsync(string companyKey, string userId);
        Task<IEnumerable<UserCompanyAccess>> GetUserCompaniesAsync(long userPkId);
    }
}
