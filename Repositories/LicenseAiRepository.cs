using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class LicenseAiRepository : ILicenseAiRepository
    {
        private readonly DapperExecutor _db;

        public LicenseAiRepository(DapperExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<LicenseAiInfo?> GetLatestAsync(string companyCd)
        {
            const string query = "CALL getlicense_ai_info(@p_COMPANY_CD)";
            return await _db.QueryFirstOrDefaultAsync<LicenseAiInfo>(
                Net_DB.Net_DB_Manager,
                query,
                new { p_COMPANY_CD = (companyCd ?? string.Empty).Trim() });
        }

        public async Task<int> MarkInitDataAsync(string companyCd)
        {
            const string query = "CALL setlicense_ai_init_data(@p_COMPANY_CD)";
            var affected = await _db.QueryFirstOrDefaultAsync<int>(
                Net_DB.Net_DB_Manager,
                query,
                new { p_COMPANY_CD = (companyCd ?? string.Empty).Trim() });
            return affected;
        }
    }
}
