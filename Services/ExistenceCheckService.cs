using System.Data;
using System.Diagnostics;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Dapper;

namespace API_AMNOTE_WEB.Services
{
    public class ExistenceCheckService : IExistenceCheckService
    {
        private const string CheckExistsProc = "check_exists";
        private const string CheckExistsCallLog =
            "CALL check_exists(@p_TABLE_NAME, @p_COMPANY_FIELD, @p_COMPANY_CD, @p_CODE_FIELD, @p_CODE_VALUE, @p_ID_FIELD, @p_EXCLUDE_ID, @p_CNT)";
        private readonly DapperExecutor _db;

        public ExistenceCheckService(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<bool> ExistsAsync(
            string tableName,
            string codeField,
            string codeValue,
            string? companyCd,
            string? companyField = "COMPANY_CD",
            string? idField = null,
            long? excludeId = null,
            bool excludeDeleted = true,
            string deleteFlagField = "ISDEL",
            Net_DB db = Net_DB.Net_DB_Company,
            string? dbName = null)
        {
            if (string.IsNullOrWhiteSpace(tableName) ||
                string.IsNullOrWhiteSpace(codeField) ||
                string.IsNullOrWhiteSpace(codeValue))
            {
                return false;
            }

            var parameters = new DynamicParameters();
            parameters.Add("p_TABLE_NAME", BuildTableSource(tableName.Trim(), excludeDeleted, deleteFlagField));
            parameters.Add("p_COMPANY_FIELD", string.IsNullOrWhiteSpace(companyCd) ? string.Empty : NormalizeField(companyField, "COMPANY_CD"));
            parameters.Add("p_COMPANY_CD", string.IsNullOrWhiteSpace(companyCd) ? null : companyCd.Trim());
            parameters.Add("p_CODE_FIELD", NormalizeField(codeField, nameof(codeField)));
            parameters.Add("p_CODE_VALUE", codeValue.Trim());
            parameters.Add("p_ID_FIELD", string.IsNullOrWhiteSpace(idField) ? string.Empty : idField.Trim());
            parameters.Add("p_EXCLUDE_ID", excludeId);
            parameters.Add("p_CNT", dbType: DbType.Int32, direction: ParameterDirection.Output);

            using var connection = _db.GetOpenConnection(db, dbName);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // MySqlConnector only allows Output params with StoredProcedure (not Text/CALL ...).
                await connection.ExecuteAsync(
                    CheckExistsProc,
                    parameters,
                    commandType: CommandType.StoredProcedure);
            }
            finally
            {
                stopwatch.Stop();
                Common.LogQuery(CheckExistsCallLog, parameters, stopwatch.ElapsedMilliseconds);
            }

            return parameters.Get<int>("p_CNT") > 0;
        }

        private static string BuildTableSource(string tableName, bool excludeDeleted, string deleteFlagField)
        {
            if (!excludeDeleted)
            {
                return tableName;
            }

            var normalizedDeleteFlag = NormalizeField(deleteFlagField, nameof(deleteFlagField));
            return $"(SELECT * FROM {tableName} WHERE IFNULL({normalizedDeleteFlag}, '0') = '0') SRC";
        }

        private static string NormalizeField(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} is required", parameterName);
            }

            return value.Trim();
        }
    }
}
