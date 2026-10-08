using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IUserSettingRepository
    {
        Task<IReadOnlyList<UserSettingInfo>> GetCandidatesAsync(string companyCd, string userId, string? keyName = null);
        Task<UserSettingInfo?> GetExactAsync(string companyCd, string userId, string keyName);
        Task<int> UpsertAsync(string companyCd, string userId, string keyName, string value, string note);
        Task<int> DeleteAsync(string companyCd, string userId, string keyName);
    }
}
