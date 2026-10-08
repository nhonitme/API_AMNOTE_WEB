using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IUserInfoRepository
    {
        Task<IEnumerable<UserInfo>> GetUserInfoAsync(string companyCd, long? userPkId = null, string? userId = null);
        Task<int> SetUserInfoAsync(string companyCd, string userId, UserInfoRequest request);
        Task<int> DeleteUserInfoAsync(string companyCd, string userId, List<long> userPkIds);
        Task<bool> UserIdExistsAsync(string companyCd, string userId, long? userPkId = null);
    }
}
