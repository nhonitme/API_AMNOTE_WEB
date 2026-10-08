using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using MySqlConnector;
using System.Data;

namespace API_AMNOTE_WEB.Services
{
    public class DapperContext : IDapperContext
    {
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private readonly int _commandTimeoutSeconds;

        public DapperContext(IConfiguration cfg, ICompanyDatabaseResolver companyDatabaseResolver)
        {
            _companyDatabaseResolver = companyDatabaseResolver;
            _commandTimeoutSeconds = cfg.GetValue("Database:CommandTimeoutSeconds", 300);
        }

        public IDbConnection CreateConnection(Net_DB _Net_DB, string? DBName = null)
        {
            string _connectionString = "";

            switch (_Net_DB)
            {
                case Net_DB.Net_DB_Manager:
                    _connectionString = Common.getManagerDBConnectStr(_commandTimeoutSeconds);
                    break;

                case Net_DB.Net_DB_Company:
                    DBName = string.IsNullOrWhiteSpace(DBName)
                        ? _companyDatabaseResolver.ResolveDatabaseName(Common.GetCompanyCode())
                        : DBName;
                    _connectionString = Common.getCompanyDBConnectStr(DBName, _commandTimeoutSeconds);
                    break;
            }

            return new MySqlConnection(_connectionString);
        }
    }
}
