using System.Data;
using API_AMNOTE_WEB.Models.DTOs;
using DevExpress.XtraReports.UI;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IConfiguredReportService
    {
        Task<XtraReport> BuildReportAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            CancellationToken cancellationToken = default);

        Task<ConfiguredReportPreviewDto> BuildPreviewAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            CancellationToken cancellationToken = default);

        Task<DataTable> ExecuteReportDataTableInSessionAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            IDbConnection connection,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default);
    }
}
