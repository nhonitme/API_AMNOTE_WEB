using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IOpeningBalanceRepository
    {
        Task<IEnumerable<BeforeStateDepartment>> GetDepartmentsAsync(string companyCd, string? openYmd = null, string? p_KEYWORD = "");
        Task<int> SaveDepartmentsAsync(List<BeforeStateDepartment> records, string? DBName = null, string? companyCd = null, string? userId = null);

        Task<IEnumerable<BeforeStateCustomer>> GetCustomersAsync(string companyCd, string? openYmd = null, string? p_KEYWORD = "");
        Task<int> SaveCustomersAsync(List<BeforeStateCustomer> records, string? DBName = null, string? companyCd = null, string? userId = null);

        Task<IEnumerable<BeforeStateBank>> GetBanksAsync(string companyCd, string? openYmd = null, string? p_KEYWORD = "");
        Task<int> SaveBanksAsync(List<BeforeStateBank> records, string? DBName = null, string? companyCd = null, string? userId = null);

        Task<IEnumerable<BeforeState>> GetBeforeStatesAsync(string companyCd, string? openYmd = null, string? p_KEYWORD = "");
        Task<int> SaveBeforeStatesAsync(List<BeforeState> records, string ? DBName = null, string? companyCd = null, string ? userId = null);
        Task<IReadOnlyList<EtcInfo>> GetEligibleAccountOptionsAsync(string companyCd, string? lang = null, string? databaseName = null);
        Task<IReadOnlyList<EtcInfo>> GetEligibleCustomerAccountOptionsAsync(string companyCd, string? lang = null, string? databaseName = null);
        Task<IReadOnlyList<EtcInfo>> GetEligibleBankAccountOptionsAsync(string companyCd, string? lang = null, string? databaseName = null);
        Task<IReadOnlyList<EtcInfo>> GetEligibleDepartmentAccountOptionsAsync(string companyCd, string? lang = null, string? databaseName = null);

        Task<IEnumerable<OpeningBalanceSummary>> GetOpeningBalanceSummaryAsync(string companyCd, string? openYmd = null);

        /// <summary>
        /// Lấy năm đầu kỳ kế toán của công ty.
        /// Store có thể đọc từ sys_config hoặc bảng cấu hình công ty.
        /// </summary>
        Task<int> GetFiscalStartYmdAsync(string companyCd, string? sDBName = null);
    }
}
