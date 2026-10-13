using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class UserProfileService : IUserProfileService
    {
        private const string CacheScope = "user-profile";
        private readonly IUserProfileRepository _repository;
        private readonly IMasterDataCacheService _cache;

        public UserProfileService(IUserProfileRepository repository, IMasterDataCacheService cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public Task<UserProfile?> GetUserProfileAsync(string companyCd, string userId)
            => _cache.GetOrCreateAsync<UserProfile?>(CacheScope, companyCd, userId,
                () => _repository.GetUserProfileAsync(companyCd, userId));

        public async Task<int> UpdateUserProfileAsync(string companyCd, string modifierUserId, UserProfileUpdateRequest request)
        {
            var result = await _repository.UpdateUserProfileAsync(companyCd, modifierUserId, request);
            await ClearCacheAsync();
            return result;
        }

        public Task<int> ChangePasswordAsync(string companyCd, string userId, string modifierUserId, string encryptedPassword)
            => _repository.ChangePasswordAsync(companyCd, userId, modifierUserId, encryptedPassword);

        // user_info fields are shared across companies, so invalidate every partition.
        public async Task ClearCacheAsync() => await _cache.ClearAsync(CacheScope);
    }
}
