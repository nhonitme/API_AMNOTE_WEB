using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class LoginRepository : ILoginRepository
    {
        private readonly DapperExecutor _db;

        public LoginRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<LoginInfo?> LoginAsync(string companyKey, string userId)
        {
            const string query = "CALL login(@p_COMPANY_KEY, @p_USERID)";
            var rows = await _db.QueryAsync<LoginInfo>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_KEY = companyKey,
                p_USERID = userId
            });

            return rows.FirstOrDefault();
        }

        public async Task<IEnumerable<UserCompanyAccess>> GetUserCompaniesAsync(long userPkId)
        {
            const string query = "CALL get_user_companies(@p_USER_PK_ID)";
            return await _db.QueryAsync<UserCompanyAccess>(Net_DB.Net_DB_Manager, query, new
            {
                p_USER_PK_ID = userPkId
            });
        }
    }
}
