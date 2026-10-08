using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class ProductInfoRepository : IProductInfoRepository
    {
        private const string GetProductInfoQuery = "CALL getProductInfo(@p_COMPANY_CD, @p_PRODUCT_ID)";
        private const string SetProductInfoQuery = @"CALL setProductInfo(
            @p_PRODUCT_ID,
            @p_COMPANY_CD,
            @p_PRODUCT_CD,
            @p_PRODUCT_NM_VIET,
            @p_PRODUCT_NM_ENG,
            @p_PRODUCT_NM_KOR,
            @p_PRODUCT_NM_CHINA,
            @p_PRODUCT_KIND_ID,
            @p_UNIT_ID,
            @p_STORE_ID,
            @p_DIVISION,
            @p_SUMMARY,
            @p_USERID
        )";
        private const string DeleteProductInfoQuery = "CALL delProductInfo(@p_COMPANY_CD, @p_PRODUCT_ID, @p_USERID)";

        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public ProductInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<ProductInfoDto>> GetProductInfoAsync(string companyCd, int? productId = null)
        {
            return await _db.QueryAsync<ProductInfoDto>(Net_DB.Net_DB_Company, GetProductInfoQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_ID = productId ?? 0
            });
        }

        public Task<int> SetProductInfoAsync(DapperSession session, string companyCd, string userId, ProductInfoDto productInfo)
        {
            return session.QuerySingleAsync<int>(SetProductInfoQuery, new
            {
                p_PRODUCT_ID = productInfo.PRODUCT_ID,
                p_COMPANY_CD = companyCd,
                p_PRODUCT_CD = productInfo.PRODUCT_CD,
                p_PRODUCT_NM_VIET = productInfo.PRODUCT_NM_VIET,
                p_PRODUCT_NM_ENG = productInfo.PRODUCT_NM_ENG,
                p_PRODUCT_NM_KOR = productInfo.PRODUCT_NM_KOR,
                p_PRODUCT_NM_CHINA = productInfo.PRODUCT_NM_CHINA,
                p_PRODUCT_KIND_ID = productInfo.PRODUCT_KIND_ID is > 0 ? productInfo.PRODUCT_KIND_ID : null,
                p_UNIT_ID = productInfo.UNIT_ID is > 0 ? productInfo.UNIT_ID : null,
                p_STORE_ID = productInfo.STORE_ID is > 0 ? productInfo.STORE_ID : null,
                p_DIVISION = productInfo.DIVISION,
                p_SUMMARY = productInfo.SUMMARY,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<ProductInfoDto> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            const int chunkSize = 200;
            var inserted = 0;
            for (var offset = 0; offset < records.Count; offset += chunkSize)
            {
                var end = Math.Min(offset + chunkSize, records.Count);
                inserted += await InsertChunkAsync(session, companyCd, userId, records, offset, end);
            }

            return inserted;
        }

        private static async Task<int> InsertChunkAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<ProductInfoDto> records,
            int startInclusive,
            int endExclusive)
        {
            var count = endExclusive - startInclusive;
            if (count <= 0)
                return 0;

            var parameters = new DynamicParameters();
            parameters.Add("p_COMPANY_CD", companyCd);
            parameters.Add("p_USERID", userId);

            var values = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var record = records[startInclusive + i];
                var cd = Common.NormalizeRequiredText(record.PRODUCT_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("PRODUCT_CD is required");

                var unitId = record.UNIT_ID is > 0 ? record.UNIT_ID : null;
                if (unitId is null)
                    throw new InvalidOperationException("UNIT_ID is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @nmV{i}, @nmE{i}, @nmK{i}, @nmC{i}, @kind{i}, @unit{i}, @store{i}, @div{i}, @sum{i}, '0', @p_USERID, NOW(), @p_USERID, NOW())");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"nmV{i}", record.PRODUCT_NM_VIET);
                parameters.Add($"nmE{i}", record.PRODUCT_NM_ENG);
                parameters.Add($"nmK{i}", record.PRODUCT_NM_KOR);
                parameters.Add($"nmC{i}", record.PRODUCT_NM_CHINA);
                parameters.Add($"kind{i}", record.PRODUCT_KIND_ID is > 0 ? record.PRODUCT_KIND_ID : null);
                parameters.Add($"unit{i}", unitId);
                parameters.Add($"store{i}", record.STORE_ID is > 0 ? record.STORE_ID : null);
                parameters.Add($"div{i}", record.DIVISION);
                parameters.Add($"sum{i}", record.SUMMARY);
            }

            var sql = $@"
INSERT INTO product_info
(
  COMPANY_CD, PRODUCT_CD, PRODUCT_NM_VIET, PRODUCT_NM_ENG, PRODUCT_NM_KOR, PRODUCT_NM_CHINA,
  PRODUCT_KIND_ID, UNIT_ID, STORE_ID, DIVISION, SUMMARY, ISDEL,
  CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteProductInfoAsync(DapperSession session, string companyCd, int productId, string userId)
        {
            return session.ExecuteAsync(DeleteProductInfoQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_ID = productId,
                p_USERID = userId
            });
        }

        public Task<bool> ProductCdExistsAsync(string companyCd, string productCd, int? productId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "product_info",
                "PRODUCT_CD",
                productCd,
                companyCd,
                idField: "PRODUCT_ID",
                excludeId: productId,
                dbName: databaseName);
        }
    }
}
