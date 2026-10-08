using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IMenuRepository
    {
        Task<IEnumerable<Menu>> GetMenuByCompanyAsync(string companyCd);
    }
}
