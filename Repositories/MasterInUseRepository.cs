using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed class MasterInUseRepository : IMasterInUseRepository
    {
        private readonly DapperExecutor _db;

        public MasterInUseRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<MasterInUseResult> CheckInUseAsync(string companyCd, string masterType, long masterId, string? masterCd = null, string? databaseName = null)
        {
            const string query = "CALL sp_master_check_in_use(@p_COMPANY_CD, @p_MASTER_TYPE, @p_MASTER_ID, @p_MASTER_CD)";

            return await _db.QuerySingleAsync<MasterInUseResult>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MASTER_TYPE = masterType,
                p_MASTER_ID = masterId,
                p_MASTER_CD = masterCd ?? string.Empty
            }, databaseName);
        }
    }
}
