using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class AcclistInfoRepository : IAcclistInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public AcclistInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<AcclistInfo>> GetAcclistInfoAsync(string companyCd, int? accId = null)
        {
            const string query = "CALL getAcclistInfo(@p_COMPANY_CD, @p_ACC_ID)";

            return await _db.QueryAsync<AcclistInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_ACC_ID = accId
            });
        }

        public Task<int> SetAcclistInfoAsync(DapperSession session, string companyCd, string userId, AcclistInfoRequest request)
        {
            const string insertQuery = @"INSERT INTO acclist_info (
                COMPANY_CD, ACC_CD, ACC_PARENT_ID,
                ACCTITLE_NM_KOR, ACCTITLE_NM_ENG, ACCTITLE_NM_VIET, ACCTITLE_NM_JAPAN, ACCTITLE_NM_CHINA,
                ISCUSTOMER, ISABLETYPE, ISABLEINPUT, ISUSERADD, `LEVEL`, DECISION, ISDEL, DESTINATION_ACC_CD,
                CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
            ) VALUES (
                @p_COMPANY_CD, @p_ACC_CD, @p_ACC_PARENT_ID,
                @p_ACCTITLE_NM_KOR, @p_ACCTITLE_NM_ENG, @p_ACCTITLE_NM_VIET, @p_ACCTITLE_NM_JAPAN, @p_ACCTITLE_NM_CHINA,
                @p_ISCUSTOMER, @p_ISABLETYPE, @p_ISABLEINPUT, @p_ISUSERADD, @p_LEVEL, @p_DECISION, @p_ISDEL, @p_DESTINATION_ACC_CD,
                @p_USER_ID, CURRENT_TIMESTAMP, @p_USER_ID, CURRENT_TIMESTAMP
            )";
            const string updateQuery = @"UPDATE acclist_info SET
                ACC_CD = @p_ACC_CD, ACC_PARENT_ID = @p_ACC_PARENT_ID,
                ACCTITLE_NM_KOR = @p_ACCTITLE_NM_KOR, ACCTITLE_NM_ENG = @p_ACCTITLE_NM_ENG,
                ACCTITLE_NM_VIET = @p_ACCTITLE_NM_VIET, ACCTITLE_NM_JAPAN = @p_ACCTITLE_NM_JAPAN,
                ACCTITLE_NM_CHINA = @p_ACCTITLE_NM_CHINA,
                ISCUSTOMER = @p_ISCUSTOMER, ISABLETYPE = @p_ISABLETYPE, ISABLEINPUT = @p_ISABLEINPUT,
                ISUSERADD = @p_ISUSERADD, `LEVEL` = @p_LEVEL, DECISION = @p_DECISION,
                ISDEL = @p_ISDEL, DESTINATION_ACC_CD = @p_DESTINATION_ACC_CD,
                UPDATE_BY = @p_USER_ID, UPDATE_AT = CURRENT_TIMESTAMP
                WHERE COMPANY_CD = @p_COMPANY_CD AND ACC_ID = @p_ACC_ID";
            var query = request.ACC_ID > 0 ? updateQuery : insertQuery;

            return session.ExecuteAsync(query, new
            {
                p_ACC_ID = request.ACC_ID,
                p_COMPANY_CD = companyCd,
                p_ACC_CD = request.ACC_CD,
                p_ACC_PARENT_ID = request.ACC_PARENT_ID,
                p_ACCTITLE_NM_KOR = request.ACCTITLE_NM_KOR,
                p_ACCTITLE_NM_ENG = request.ACCTITLE_NM_ENG,
                p_ACCTITLE_NM_VIET = request.ACCTITLE_NM_VIET,
                p_ACCTITLE_NM_JAPAN = request.ACCTITLE_NM_JAPAN,
                p_ACCTITLE_NM_CHINA = request.ACCTITLE_NM_CHINA,
                p_ISCUSTOMER = request.ISCUSTOMER,
                p_ISABLETYPE = request.ISABLETYPE,
                p_ISABLEINPUT = request.ISABLEINPUT,
                p_ISUSERADD = request.ISUSERADD,
                p_LEVEL = request.LEVEL,
                p_DECISION = request.DECISION,
                p_ISDEL = request.ISDEL,
                p_DESTINATION_ACC_CD = request.DESTINATION_ACC_CD,
                p_USER_ID = userId
            });
        }

        public Task<int> DeleteAcclistInfoAsync(DapperSession session, string companyCd, int accId, string userId)
        {
            return session.ExecuteAsync(
                "CALL delAcclistInfo(@p_COMPANY_CD, @p_ACC_ID, @p_USERID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_ACC_ID = accId,
                    p_USERID = userId
                });
        }

        public Task<bool> AccCdExistsAsync(string companyCd, string accCd, int? accId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "acclist_info",
                "ACC_CD",
                accCd,
                companyCd,
                idField: "ACC_ID",
                excludeId: accId,
                dbName: databaseName);
        }
    }
}
