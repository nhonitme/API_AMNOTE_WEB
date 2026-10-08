using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class DepartmentInfoRepository : IDepartmentInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public DepartmentInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<DepartmentInfo>> GetDepartmentInfoAsync(string companyCd, long? departmentId = null, string? departmentCd = null)
        {
            const string query = "CALL getDepartmentInfo(@p_COMPANY_CD, @p_DEPARTMENT_ID, @p_DEPARTMENT_CD)";

            return await _db.QueryAsync<DepartmentInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_DEPARTMENT_ID = departmentId,
                p_DEPARTMENT_CD = departmentCd
            });
        }

        public Task<int> SetDepartmentInfoAsync(DapperSession session, string companyCd, string userId, DepartmentInfoRequest request)
        {
            const string query = @"CALL setDepartmentInfo(
                @p_DEPARTMENT_ID,
                @p_COMPANY_CD,
                @p_DEPARTMENT_CD,
                @p_PARENT_CD,
                @p_DEP_NAME_KOR,
                @p_DEP_NAME_ENG,
                @p_DEP_NAME_VIET,
                @p_DEP_NAME_CHINA,
                @p_ISDEL,
                @p_USERID)";

            return session.ExecuteAsync(query, new
            {
                p_DEPARTMENT_ID = request.DEPARTMENT_ID ?? 0,
                p_COMPANY_CD = companyCd,
                p_DEPARTMENT_CD = request.DEPARTMENT_CD,
                p_PARENT_CD = request.PARENT_CD,
                p_DEP_NAME_KOR = request.DEP_NAME_KOR,
                p_DEP_NAME_ENG = request.DEP_NAME_ENG,
                p_DEP_NAME_VIET = request.DEP_NAME_VIET,
                p_DEP_NAME_CHINA = request.DEP_NAME_CHINA,
                p_ISDEL = string.IsNullOrWhiteSpace(request.ISDEL) ? "0" : request.ISDEL,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<DepartmentInfoRequest> records)
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
            IReadOnlyList<DepartmentInfoRequest> records,
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
                var cd = Common.NormalizeRequiredText(record.DEPARTMENT_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("DEPARTMENT_CD is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @parentCd{i}, @nmK{i}, @nmE{i}, @nmV{i}, @nmC{i}, NOW(), @p_USERID, NOW(), @p_USERID, @isdel{i})");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"parentCd{i}", Common.NormalizeNullableText(record.PARENT_CD) ?? string.Empty);
                parameters.Add($"nmK{i}", record.DEP_NAME_KOR);
                parameters.Add($"nmE{i}", record.DEP_NAME_ENG);
                parameters.Add($"nmV{i}", record.DEP_NAME_VIET);
                parameters.Add($"nmC{i}", record.DEP_NAME_CHINA);
                parameters.Add($"isdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
            }

            var sql = $@"
INSERT INTO department_info
(
  COMPANY_CD, DEPARTMENT_CD, PARENT_CD, DEP_NAME_KOR, DEP_NAME_ENG, DEP_NAME_VIET, DEP_NAME_CHINA,
  UPDATE_AT, UPDATE_BY, CREATE_AT, CREATE_BY, ISDEL
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteDepartmentInfoAsync(DapperSession session, string companyCd, long departmentId, string userId)
        {
            return session.ExecuteAsync(
                "CALL delDepartmentInfo(@p_COMPANY_CD, @p_DEPARTMENT_ID, @p_USERID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_DEPARTMENT_ID = departmentId,
                    p_USERID = userId
                });
        }

        public async Task<IEnumerable<DepartmentLookupItem>> GetDepartmentLookupAsync(string companyCd, string lang = "VIET")
        {
            var departments = await GetDepartmentInfoAsync(companyCd, null, null);

            return departments
                .OrderBy(item => item.DEPARTMENT_CD)
                .Select(item => new DepartmentLookupItem
                {
                    VALUE = item.DEPARTMENT_CD,
                    TEXT = lang.ToUpperInvariant() switch
                    {
                        "ENG" => string.IsNullOrWhiteSpace(item.DEP_NAME_ENG) ? item.DEPARTMENT_CD : item.DEP_NAME_ENG.Trim(),
                        "KOR" => string.IsNullOrWhiteSpace(item.DEP_NAME_KOR) ? item.DEPARTMENT_CD : item.DEP_NAME_KOR.Trim(),
                        "CHN" => string.IsNullOrWhiteSpace(item.DEP_NAME_CHINA) ? item.DEPARTMENT_CD : item.DEP_NAME_CHINA.Trim(),
                        _ => string.IsNullOrWhiteSpace(item.DEP_NAME_VIET) ? item.DEPARTMENT_CD : item.DEP_NAME_VIET.Trim()
                    }
                });
        }

        public Task<bool> DepartmentCdExistsAsync(string companyCd, string departmentCd, long? departmentId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "department_info",
                "DEPARTMENT_CD",
                departmentCd,
                companyCd,
                idField: "DEPARTMENT_ID",
                excludeId: departmentId,
                dbName: databaseName);
        }
    }
}
