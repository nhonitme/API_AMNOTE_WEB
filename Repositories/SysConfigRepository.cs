using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Repositories
{
    public class SysConfigRepository : ISysConfigRepository
    {
        private readonly DapperExecutor _db;

        public SysConfigRepository(DapperExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IEnumerable<SysConfig>> GetRuntimeSysConfigsAsync(string companyCd)
        {
            const string query = "CALL getsys_config_runtime(@p_COMPANY_CD)";
            return await _db.QueryAsync<SysConfig>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_CD = string.IsNullOrWhiteSpace(companyCd) ? string.Empty : companyCd.Trim()
            });
        }
    }
}
