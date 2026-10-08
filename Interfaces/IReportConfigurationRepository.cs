using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IReportConfigurationRepository
    {
        Task<ReportConfigurationInfo?> GetCompanyReportConfigurationAsync(string companyCd, string? reportCode = null, string? menuCode = null);
        Task<IEnumerable<CompanyReportElementInfo>> GetCompanyReportElementsAsync(string companyCd, string reportKey, string? reportCode = null);
        Task<IEnumerable<ReportColumnLayoutInfo>> GetReportColumnLayoutAsync(string companyCd, string reportKey, string? reportCode = null);
        Task ClearCompanyReportConfigurationCacheAsync(string companyCd);
    }
}
