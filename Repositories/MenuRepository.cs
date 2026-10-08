using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class MenuRepository : IMenuRepository
    {
        private readonly DapperExecutor _db;

        public MenuRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Menu>> GetMenuByCompanyAsync(string companyCd)
        {
            var query = "CALL sp_get_menu_by_company(@p_COMPANY_CD)";
            return await _db.QueryAsync<Menu>(Net_DB.Net_DB_Manager, query, new { p_COMPANY_CD = companyCd });
        }
    }
}