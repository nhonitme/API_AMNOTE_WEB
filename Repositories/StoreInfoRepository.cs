using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class StoreInfoRepository : IStoreInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public StoreInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<StoreInfo>> GetStoreInfoAsync(string companyCd, int? storeId = null)
        {
            const string query = "CALL getStoreInfo(@p_COMPANY_CD, @p_STORE_ID)";

            return await _db.QueryAsync<StoreInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_STORE_ID = storeId
            });
        }

        public Task<int> SetStoreInfoAsync(DapperSession session, string companyCd, string userId, StoreInfoRequest request)
        {
            const string query = @"CALL setStoreInfo(
                @p_STORE_ID,
                @p_COMPANY_CD,
                @p_STORE_CD,
                @p_STORE_NM_VIET,
                @p_STORE_NM_ENG,
                @p_STORE_NM_KOR,
                @p_STORE_NM_CHINA,
                @p_STORE_KIND_ID,
                @p_USERID)";

            return session.ExecuteAsync(query, new
            {
                p_STORE_ID = request.STORE_ID,
                p_COMPANY_CD = companyCd,
                p_STORE_CD = request.STORE_CD,
                p_STORE_NM_VIET = request.STORE_NM_VIET,
                p_STORE_NM_ENG = request.STORE_NM_ENG,
                p_STORE_NM_KOR = request.STORE_NM_KOR,
                p_STORE_NM_CHINA = request.STORE_NM_CHINA,
                p_STORE_KIND_ID = request.STORE_KIND_ID,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<StoreInfoRequest> records)
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
            IReadOnlyList<StoreInfoRequest> records,
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
                var cd = Common.NormalizeRequiredText(record.STORE_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("STORE_CD is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @nmV{i}, @nmE{i}, @nmK{i}, @nmC{i}, @kind{i}, @p_USERID, CURRENT_TIMESTAMP, @p_USERID, CURRENT_TIMESTAMP)");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"nmV{i}", record.STORE_NM_VIET ?? string.Empty);
                parameters.Add($"nmE{i}", record.STORE_NM_ENG ?? string.Empty);
                parameters.Add($"nmK{i}", record.STORE_NM_KOR ?? string.Empty);
                parameters.Add($"nmC{i}", record.STORE_NM_CHINA ?? string.Empty);
                parameters.Add($"kind{i}", record.STORE_KIND_ID);
            }

            var sql = $@"
INSERT INTO store_info
(
  COMPANY_CD, STORE_CD, STORE_NM_VIET, STORE_NM_ENG, STORE_NM_KOR, STORE_NM_CHINA,
  STORE_KIND_ID, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteStoreInfoAsync(DapperSession session, string companyCd, int storeId, string userId)
        {
            return session.ExecuteAsync(
                "CALL delStoreInfo(@p_COMPANY_CD, @p_STORE_ID, @p_USERID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_STORE_ID = storeId,
                    p_USERID = userId
                });
        }

        public Task<bool> StoreCdExistsAsync(string companyCd, string storeCd, int? storeId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "store_info",
                "STORE_CD",
                storeCd,
                companyCd,
                idField: "STORE_ID",
                excludeId: storeId,
                dbName: databaseName);
        }
    }
}
