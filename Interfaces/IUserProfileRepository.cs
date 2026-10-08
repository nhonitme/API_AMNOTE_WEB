using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IUserProfileRepository
    {
        Task<UserProfile?> GetUserProfileAsync(string companyCd, string userId);
        Task<int> UpdateUserProfileAsync(string companyCd, string modifierUserId, UserProfileUpdateRequest request);
        Task<int> ChangePasswordAsync(string companyCd, string userId, string modifierUserId, string encryptedPassword);
    }
}
