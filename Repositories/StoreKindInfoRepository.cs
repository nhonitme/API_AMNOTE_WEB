using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class StoreKindInfoRepository : IStoreKindInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public StoreKindInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<StoreKindInfo>> GetStoreKindInfoAsync(string companyCd, int? storeKindId = null, string? databaseName = null)
        {
            const string query = "CALL getStoreKindInfo(@p_COMPANY_CD, @p_STORE_KIND_ID)";

            return await _db.QueryAsync<StoreKindInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_STORE_KIND_ID = storeKindId
            }, databaseName);
        }

        public Task<int> SetStoreKindInfoAsync(DapperSession session, string companyCd, string userId, StoreKindInfoRequest request)
        {
            const string query = @"CALL setStoreKindInfo(
                @p_STORE_KIND_ID,
                @p_COMPANY_CD,
                @p_STORE_KIND_CD,
                @p_STORE_KIND_NM_VIET,
                @p_STORE_KIND_NM_ENG,
                @p_STORE_KIND_NM_KOR,
                @p_STORE_KIND_NM_CHINA,
                @p_USERID)";

            return session.ExecuteAsync(query, new
            {
                p_STORE_KIND_ID = request.STORE_KIND_ID,
                p_COMPANY_CD = companyCd,
                p_STORE_KIND_CD = request.STORE_KIND_CD,
                p_STORE_KIND_NM_VIET = request.STORE_KIND_NM_VIET,
                p_STORE_KIND_NM_ENG = request.STORE_KIND_NM_ENG,
                p_STORE_KIND_NM_KOR = request.STORE_KIND_NM_KOR,
                p_STORE_KIND_NM_CHINA = request.STORE_KIND_NM_CHINA,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<StoreKindInfoRequest> records)
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
            IReadOnlyList<StoreKindInfoRequest> records,
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
                var cd = Common.NormalizeRequiredText(record.STORE_KIND_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("STORE_KIND_CD is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @nmV{i}, @nmE{i}, @nmK{i}, @nmC{i}, @p_USERID, CURRENT_TIMESTAMP, @p_USERID, CURRENT_TIMESTAMP)");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"nmV{i}", record.STORE_KIND_NM_VIET ?? string.Empty);
                parameters.Add($"nmE{i}", record.STORE_KIND_NM_ENG ?? string.Empty);
                parameters.Add($"nmK{i}", record.STORE_KIND_NM_KOR ?? string.Empty);
                parameters.Add($"nmC{i}", record.STORE_KIND_NM_CHINA ?? string.Empty);
            }

            var sql = $@"
INSERT INTO store_kind_info
(
  COMPANY_CD, STORE_KIND_CD, STORE_KIND_NM_VIET, STORE_KIND_NM_ENG, STORE_KIND_NM_KOR, STORE_KIND_NM_CHINA,
  CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteStoreKindInfoAsync(DapperSession session, string companyCd, int storeKindId, string userId)
        {
            return session.ExecuteAsync(
                "CALL delStoreKindInfo(@p_COMPANY_CD, @p_STORE_KIND_ID, @p_USERID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_STORE_KIND_ID = storeKindId,
                    p_USERID = userId
                });
        }

        public async Task<IEnumerable<LookupItem>> GetStoreKindLookupAsync(string companyCd, int? storeKindId = null, string lang = "VIET")
        {
            const string query = "CALL getStoreKindInfo(@p_COMPANY_CD, @p_STORE_KIND_ID)";

            var data = await _db.QueryAsync<StoreKindInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_STORE_KIND_ID = storeKindId
            });

            var normalizedLang = Common.NormalizeLanguageCode(lang);

            return data.Select(x => new LookupItem
            {
                VALUE = x.STORE_KIND_ID,
                TEXT = normalizedLang switch
                {
                    "ENG" => string.IsNullOrWhiteSpace(x.STORE_KIND_NM_ENG) ? x.STORE_KIND_NM_VIET : x.STORE_KIND_NM_ENG,
                    "KOR" => string.IsNullOrWhiteSpace(x.STORE_KIND_NM_KOR) ? x.STORE_KIND_NM_VIET : x.STORE_KIND_NM_KOR,
                    "CHN" or "THA" => string.IsNullOrWhiteSpace(x.STORE_KIND_NM_CHINA) ? x.STORE_KIND_NM_VIET : x.STORE_KIND_NM_CHINA,
                    _ => x.STORE_KIND_NM_VIET
                }
            });
        }

        public Task<bool> StoreKindCdExistsAsync(string companyCd, string storeKindCd, int? storeKindId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "store_kind_info",
                "STORE_KIND_CD",
                storeKindCd,
                companyCd,
                idField: "STORE_KIND_ID",
                excludeId: storeKindId,
                dbName: databaseName);
        }
    }
}
