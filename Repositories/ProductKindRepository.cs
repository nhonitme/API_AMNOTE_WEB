using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class ProductKindRepository : IProductKindRepository
    {
        private const string GetProductKindQuery = "CALL getProductKind(@p_COMPANY_CD, @p_PRODUCT_KIND_ID, @p_PRODUCT_KIND_CD)";
        private const string SetProductKindQuery = @"CALL setProductKind(
            @p_PRODUCT_KIND_ID,
            @p_COMPANY_CD,
            @p_PRODUCT_KIND_CD,
            @p_PRODUCTKIND_NM_VIET,
            @p_PRODUCTKIND_NM_ENG,
            @p_PRODUCTKIND_NM_KOR,
            @p_PRODUCTKIND_NM_CHINA,
            @p_REMARK,
            @p_ISDEL,
            @p_USERID
        )";
        private const string DeleteProductKindQuery = "CALL delProductKind(@p_COMPANY_CD, @p_PRODUCT_KIND_ID, @p_USERID)";

        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public ProductKindRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<ProductKind>> GetProductKindAsync(string companyCd, int? productKindId = null, string? productKindCd = null)
        {
            return await _db.QueryAsync<ProductKind>(Net_DB.Net_DB_Company, GetProductKindQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_KIND_ID = productKindId ?? 0,
                p_PRODUCT_KIND_CD = productKindCd ?? string.Empty
            });
        }

        public Task<int> SetProductKindAsync(DapperSession session, string companyCd, string userId, ProductKind productKind)
        {
            return session.QuerySingleAsync<int>(SetProductKindQuery, new
            {
                p_PRODUCT_KIND_ID = productKind.PRODUCT_KIND_ID,
                p_COMPANY_CD = companyCd,
                p_PRODUCT_KIND_CD = productKind.PRODUCT_KIND_CD,
                p_PRODUCTKIND_NM_VIET = productKind.PRODUCTKIND_NM_VIET,
                p_PRODUCTKIND_NM_ENG = productKind.PRODUCTKIND_NM_ENG,
                p_PRODUCTKIND_NM_KOR = productKind.PRODUCTKIND_NM_KOR,
                p_PRODUCTKIND_NM_CHINA = productKind.PRODUCTKIND_NM_CHINA,
                p_REMARK = productKind.REMARK,
                p_ISDEL = productKind.ISDEL,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<ProductKind> records)
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
            IReadOnlyList<ProductKind> records,
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
                var cd = Common.NormalizeRequiredText(record.PRODUCT_KIND_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("PRODUCT_KIND_CD is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @nmV{i}, @nmE{i}, @nmK{i}, @nmC{i}, @rmk{i}, @isdel{i}, @p_USERID, NOW(), @p_USERID, NOW())");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"nmV{i}", record.PRODUCTKIND_NM_VIET);
                parameters.Add($"nmE{i}", record.PRODUCTKIND_NM_ENG);
                parameters.Add($"nmK{i}", record.PRODUCTKIND_NM_KOR);
                parameters.Add($"nmC{i}", record.PRODUCTKIND_NM_CHINA);
                parameters.Add($"rmk{i}", record.REMARK);
                parameters.Add($"isdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
            }

            var sql = $@"
INSERT INTO product_kind
(
  COMPANY_CD, PRODUCT_KIND_CD, PRODUCTKIND_NM_VIET, PRODUCTKIND_NM_ENG, PRODUCTKIND_NM_KOR, PRODUCTKIND_NM_CHINA,
  REMARK, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteProductKindAsync(DapperSession session, string companyCd, int productKindId, string userId)
        {
            return session.ExecuteAsync(DeleteProductKindQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_KIND_ID = productKindId,
                p_USERID = userId
            });
        }

        public Task<bool> ProductKindCdExistsAsync(string companyCd, string productKindCd, int? productKindId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "product_kind",
                "PRODUCT_KIND_CD",
                productKindCd,
                companyCd,
                idField: "PRODUCT_KIND_ID",
                excludeId: productKindId,
                dbName: databaseName);
        }
    }
}
