using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IProductKindRepository
    {
        Task<IEnumerable<ProductKind>> GetProductKindAsync(string companyCd, int? productKindId = null, string? productKindCd = null);

        Task<int> SetProductKindAsync(DapperSession session, string companyCd, string userId, ProductKind productKind);

        /// <summary>Excel import: multi-row INSERT (no per-row setProductKind).</summary>
        Task<int> BulkInsertNewAsync(DapperSession session, string companyCd, string userId, IReadOnlyList<ProductKind> records);

        Task<int> DeleteProductKindAsync(DapperSession session, string companyCd, int productKindId, string userId);

        Task<bool> ProductKindCdExistsAsync(string companyCd, string productKindCd, int? productKindId = null, string? databaseName = null);
    }
}
