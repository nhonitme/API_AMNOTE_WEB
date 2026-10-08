using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardKpiDto?> GetKpiAsync(string companyCd, string fromYmd, string toYmd);
        Task<IEnumerable<DashboardChartItemDto>> GetChartAsync(string companyCd, string year);
        Task<IEnumerable<DashboardTaskItemDto>> GetTasksAsync(string companyCd, string fromYmd, string toYmd);
        Task<IEnumerable<DashboardReceivableDto>> GetReceivablesAsync(string companyCd, string toYmd, int top = 10);
        Task<IEnumerable<DashboardPayableDto>> GetPayablesAsync(string companyCd, string toYmd, int top = 10);
        Task<IEnumerable<DashboardTaxDeadlineDto>> GetTaxDeadlinesAsync(string companyCd, string periodYm);
        Task<DashboardPeriodLockDto> GetPeriodLockAsync(string companyCd, string periodYm);
        Task<IEnumerable<DashboardRecentVoucherDto>> GetRecentVouchersAsync(string companyCd, string fromYmd, string toYmd, string? status, int limit = 20);
    }
}
