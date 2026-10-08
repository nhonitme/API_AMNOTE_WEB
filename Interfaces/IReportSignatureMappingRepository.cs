using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IReportSignatureMappingRepository
    {
        Task<ReportSignatureMappingInfo?> GetReportSignatureMappingAsync(string companyCd, string reportKey, string? reportCode = null);
        Task<ReportSignatureMappingInfo> SaveReportSignatureMappingAsync(string companyCd, string reportKey, string? reportCode, IReadOnlyCollection<string> signCodes, string userId);
    }
}
