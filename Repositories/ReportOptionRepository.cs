using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed class ReportOptionRepository : IReportOptionRepository
    {
        private readonly DapperExecutor _db;

        public ReportOptionRepository(DapperExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<ReportOptionInfo>> GetReportOptionsAsync(
            string companyCd,
            string reportGroupCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new ArgumentException("companyCd is required.", nameof(companyCd));
            }

            if (string.IsNullOrWhiteSpace(reportGroupCode))
            {
                throw new ArgumentException("reportGroupCode is required.", nameof(reportGroupCode));
            }

            const string query = "CALL get_sys_report_options(@p_COMPANY_CD, @p_REPORT_GROUP_CODE)";

            using var connection = _db.GetOpenConnection(Net_DB.Net_DB_Manager);
            var command = new CommandDefinition(
                query,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_REPORT_GROUP_CODE = reportGroupCode.Trim()
                },
                cancellationToken: cancellationToken);

            var rows = await connection.QueryAsync<ReportOptionInfo>(command);
            return rows.AsList();
        }
    }
}
