using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class ProductUnitRepository : IProductUnitRepository
    {
        private const string GetProductUnitQuery = "CALL getProductUnit(@p_COMPANY_CD, @p_UNIT_ID, @p_UNIT_CD)";
        private const string SetProductUnitQuery = @"CALL setProductUnit(
            @p_UNIT_ID,
            @p_COMPANY_CD,
            @p_UNIT_CD,
            @p_UNIT_NM,
            @p_ISDEL,
            @p_USERID
        )";
        private const string DeleteProductUnitQuery = "CALL delProductUnit(@p_COMPANY_CD, @p_UNIT_ID, @p_USERID)";

        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public ProductUnitRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<ProductUnit>> GetProductUnitAsync(string companyCd, int? unitId = null, string? unitCd = null)
        {
            return await _db.QueryAsync<ProductUnit>(Net_DB.Net_DB_Company, GetProductUnitQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_UNIT_ID = unitId ?? 0,
                p_UNIT_CD = unitCd ?? string.Empty
            });
        }

        public Task<int> SetProductUnitAsync(DapperSession session, string companyCd, string userId, ProductUnit productUnit)
        {
            return session.QuerySingleAsync<int>(SetProductUnitQuery, new
            {
                p_UNIT_ID = productUnit.UNIT_ID,
                p_COMPANY_CD = companyCd,
                p_UNIT_CD = productUnit.UNIT_CD,
                p_UNIT_NM = productUnit.UNIT_NM,
                p_ISDEL = productUnit.ISDEL,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<ProductUnit> records)
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
            IReadOnlyList<ProductUnit> records,
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
                var cd = Common.NormalizeRequiredText(record.UNIT_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("UNIT_CD is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @nm{i}, @isdel{i}, @p_USERID, NOW(), @p_USERID, NOW())");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"nm{i}", record.UNIT_NM);
                parameters.Add($"isdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
            }

            var sql = $@"
INSERT INTO product_unit
(
  COMPANY_CD, UNIT_CD, UNIT_NM, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteProductUnitAsync(DapperSession session, string companyCd, int unitId, string userId)
        {
            return session.ExecuteAsync(DeleteProductUnitQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_UNIT_ID = unitId,
                p_USERID = userId
            });
        }

        public Task<bool> ProductUnitCdExistsAsync(string companyCd, string unitCd, int? unitId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "product_unit",
                "UNIT_CD",
                unitCd,
                companyCd,
                idField: "UNIT_ID",
                excludeId: unitId,
                dbName: databaseName);
        }
    }
}
