using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface IProductInfoService
    {
        Task<IEnumerable<ProductInfoDto>> GetListAsync(string companyCd, int? productId = null, string? productCd = null);
        Task<ProductInfoDto?> GetByIdAsync(string companyCd, int productId);
        Task<ProductInfoDto> CreateAsync(string companyCd, string userId, ProductInfo request);
        Task<ProductInfoDto> UpdateAsync(string companyCd, string userId, int productId, ProductInfoDto request);
        Task<int> DeleteAsync(string companyCd, string userId, List<int> productIds);
        Task<bool> CodeExistsAsync(string companyCd, string productCd, int? excludeId = null, string? databaseName = null, string? lang = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<ProductInfoDto> records, string? databaseName = null, string? lang = null);
    }
}
