using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System;
using System.Linq;
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

        public async Task<IEnumerable<SysConfig>> GetSysConfigsAsync(string companyCd, string? configGroup = null, string? configKey = null, bool activeOnly = true)
        {
            const string query = "CALL getsys_config(@p_COMPANY_CD, @p_CONFIG_GROUP, @p_CONFIG_KEY, @p_ACTIVE_ONLY)";
            return await _db.QueryAsync<SysConfig>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_CD = companyCd,
                p_CONFIG_GROUP = string.IsNullOrWhiteSpace(configGroup) ? null : configGroup.Trim(),
                p_CONFIG_KEY = string.IsNullOrWhiteSpace(configKey) ? null : configKey.Trim(),
                p_ACTIVE_ONLY = activeOnly ? "1" : "0"
            });
        }

        public async Task<SysConfig?> GetSysConfigAsync(string companyCd, string configGroup, string configKey, bool activeOnly = true)
        {
            var items = await GetSysConfigsAsync(companyCd, configGroup, configKey, activeOnly);
            return items.FirstOrDefault();
        }
    }
}
