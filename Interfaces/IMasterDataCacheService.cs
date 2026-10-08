namespace API_AMNOTE_WEB.Interfaces
{
    public interface IMasterDataCacheService
    {
        Task<T> GetOrCreateAsync<T>(string scope, string companyCd, string key, Func<Task<T>> factory);

        Task<T> GetOrCreateGlobalAsync<T>(string scope, string key, Func<Task<T>> factory);

        Task<T> RefreshAsync<T>(string scope, string companyCd, string key, Func<Task<T>> factory);

        Task<T> RefreshGlobalAsync<T>(string scope, string key, Func<Task<T>> factory);

        Task<int> ClearAsync(string scope, string? companyCd = null);

        Task<int> ClearGlobalAsync(string scope);

        Task<int> ClearAllAsync();
    }
}
