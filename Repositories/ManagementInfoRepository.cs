using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class ManagementInfoRepository : IManagementInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public ManagementInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<ManagementInfo>> GetManagementInfoAsync(string companyCd, long? managementId = null, string? mgCd = null)
        {
            const string query = "CALL getManagementInfo(@p_COMPANY_CD, @p_MG_ID, @p_MG_CD)";

            return await _db.QueryAsync<ManagementInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MG_ID = managementId,
                p_MG_CD = mgCd
            });
        }

        public Task<int> SetManagementInfoAsync(DapperSession session, string companyCd, string userId, ManagementInfoRequest request)
        {
            const string query = @"CALL setManagementInfo(
                @p_MG_ID,
                @p_COMPANY_CD,
                @p_MG_CD,
                @p_MG_DESC_KOR,
                @p_MG_DESC_ENG,
                @p_MG_DESC_VIET,
                @p_MG_CD_ROOT,
                @p_ISDEL,
                @p_USERID)";

            return session.ExecuteAsync(query, new
            {
                p_MG_ID = request.MG_ID ?? 0,
                p_COMPANY_CD = companyCd,
                p_MG_CD = request.MG_CD,
                p_MG_DESC_KOR = request.MG_DESC_KOR,
                p_MG_DESC_ENG = request.MG_DESC_ENG,
                p_MG_DESC_VIET = request.MG_DESC_VIET,
                p_MG_CD_ROOT = request.MG_CD_ROOT,
                p_ISDEL = request.ISDEL,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<ManagementInfoRequest> records)
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
            IReadOnlyList<ManagementInfoRequest> records,
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
                var cd = Common.NormalizeRequiredText(record.MG_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("MG_CD is required");

                values.Add(
                    $"(@cd{i}, @descK{i}, @descE{i}, @descV{i}, @isdel{i}, NOW(), @p_USERID, NOW(), @p_USERID, @root{i}, @p_COMPANY_CD)");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"descK{i}", record.MG_DESC_KOR);
                parameters.Add($"descE{i}", record.MG_DESC_ENG);
                parameters.Add($"descV{i}", record.MG_DESC_VIET);
                parameters.Add($"isdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
                parameters.Add($"root{i}", record.MG_CD_ROOT?.Trim() ?? string.Empty);
            }

            var sql = $@"
INSERT INTO management_info
(
  MG_CD, MG_DESC_KOR, MG_DESC_ENG, MG_DESC_VIET, ISDEL,
  CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY, MG_CD_ROOT, COMPANY_CD
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteManagementInfoAsync(DapperSession session, string companyCd, long managementId, string userId)
        {
            return session.ExecuteAsync(
                "CALL delManagementInfo(@p_COMPANY_CD, @p_MG_ID, @p_USERID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_MG_ID = managementId,
                    p_USERID = userId
                });
        }

        public Task<bool> MgCdExistsAsync(string companyCd, string mgCd, long? managementId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "management_info",
                "MG_CD",
                mgCd,
                companyCd,
                idField: "MG_ID",
                excludeId: managementId,
                dbName: databaseName);
        }
    }
}
