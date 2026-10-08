using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class BankInfoRepository : IBankInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public BankInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<BankInfo>> GetBankInfoAsync(string companyCd, long? bankId = null, string? bankCd = null)
        {
            const string query = "CALL getBankInfo(@p_COMPANY_CD, @p_BANK_ID, @p_BANK_CD)";

            return await _db.QueryAsync<BankInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_BANK_ID = bankId,
                p_BANK_CD = bankCd
            });
        }

        public Task<int> SetBankInfoAsync(DapperSession session, string companyCd, string userId, BankInfoRequest request)
        {
            const string query = @"CALL setBankInfo(
                @p_BANK_ID,
                @p_COMPANY_CD,
                @p_BANK_CD,
                @p_BANK_NM,
                @p_ACC_CD,
                @p_PASSBOOK_NM,
                @p_ACCOUNT_NUM,
                @p_CITAD_CODE,
                @p_REMARK,
                @p_ISDEL,
                @p_USERID)";

            return session.ExecuteAsync(query, new
            {
                p_BANK_ID = request.BANK_ID ?? 0,
                p_COMPANY_CD = companyCd,
                p_BANK_CD = request.BANK_CD,
                p_BANK_NM = request.BANK_NM,
                p_ACC_CD = request.ACC_CD,
                p_PASSBOOK_NM = request.PASSBOOK_NM,
                p_ACCOUNT_NUM = request.ACCOUNT_NUM,
                p_CITAD_CODE = request.CITAD_CODE,
                p_REMARK = request.REMARK,
                p_ISDEL = request.ISDEL,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<BankInfoRequest> records)
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
            IReadOnlyList<BankInfoRequest> records,
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
                var cd = Common.NormalizeRequiredText(record.BANK_CD);
                if (string.IsNullOrWhiteSpace(cd))
                    throw new InvalidOperationException("BANK_CD is required");

                values.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @nm{i}, @acc{i}, @pb{i}, @anum{i}, @citad{i}, @rmk{i}, @isdel{i}, NOW(), NOW(), @p_USERID, @p_USERID)");
                parameters.Add($"cd{i}", cd);
                parameters.Add($"nm{i}", record.BANK_NM);
                parameters.Add($"acc{i}", record.ACC_CD);
                parameters.Add($"pb{i}", record.PASSBOOK_NM);
                parameters.Add($"anum{i}", record.ACCOUNT_NUM);
                parameters.Add($"citad{i}", record.CITAD_CODE);
                parameters.Add($"rmk{i}", record.REMARK);
                parameters.Add($"isdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
            }

            var sql = $@"
INSERT INTO bank_info
(
  COMPANY_CD, BANK_CD, BANK_NM, ACC_CD, PASSBOOK_NM, ACCOUNT_NUM, CITAD_CODE, REMARK,
  ISDEL, CREATE_AT, UPDATE_AT, CREATE_BY, UPDATE_BY
)
VALUES {string.Join(",\n", values)}";

            return await session.ExecuteAsync(sql, parameters);
        }

        public Task<int> DeleteBankInfoAsync(DapperSession session, string companyCd, long bankId)
        {
            return session.ExecuteAsync(
                "CALL delBankInfo(@p_COMPANY_CD, @p_BANK_ID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_BANK_ID = bankId
                });
        }

        public Task<bool> BankCdExistsAsync(string companyCd, string bankCd, long? bankId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "bank_info",
                "BANK_CD",
                bankCd,
                companyCd,
                idField: "BANK_ID",
                excludeId: bankId,
                dbName: databaseName);
        }
    }
}
