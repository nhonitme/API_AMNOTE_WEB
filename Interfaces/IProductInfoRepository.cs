using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IProductInfoRepository
    {
        Task<IEnumerable<ProductInfoDto>> GetProductInfoAsync(string companyCd, int? productId = null);

        Task<int> SetProductInfoAsync(DapperSession session, string companyCd, string userId, ProductInfoDto productInfo);

        /// <summary>Excel import: multi-row INSERT (no per-row setProductInfo).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<ProductInfoDto> records);

        /// <summary>
        /// Link one finished product to a matching active cost object within the same company.
        /// Returns false if either record is missing or the codes do not match.
        /// </summary>
        Task<bool> LinkDepartmentAsync(
            DapperSession session, string companyCd, int productId, string productCd,
            long departmentId, string divisionPrefix, string userId);

        Task<int> DeleteProductInfoAsync(DapperSession session, string companyCd, int productId, string userId);

        Task<bool> ProductCdExistsAsync(string companyCd, string productCd, int? productId = null, string? databaseName = null);
    }
}
