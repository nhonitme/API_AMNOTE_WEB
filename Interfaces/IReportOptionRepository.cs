using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IReportOptionRepository
    {
        Task<IReadOnlyList<ReportOptionInfo>> GetReportOptionsAsync(
            string companyCd,
            string reportGroupCode,
            CancellationToken cancellationToken = default);
    }
}
