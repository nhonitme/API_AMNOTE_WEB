using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICustomerInfoCustomerExtRepository
    {
        Task<IEnumerable<CustomerInfoCustomerExt>> GetCustomerInfoCustomerExtAsync(string companyCd, long? customerId = null, string? customerCd = null);

        Task<int> SetCustomerInfoCustomerExtAsync(DapperSession session, string companyCd, string userId, CustomerInfoCustomerExtRequest request);

        /// <summary>
        /// Excel-import path: multi-row INSERT into customer_info + customer_ext (no per-row SP).
        /// Caller must validate uniqueness first; rows are treated as new inserts (CUSTOMER_ID = 0).
        /// </summary>
        Task<int> BulkInsertNewCustomersAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<CustomerInfoCustomerExtRequest> records);

        Task<int> DeleteCustomerInfoCustomerExtAsync(DapperSession session, string companyCd, long customerId, string userId);

        Task<bool> CustomerCdExistsAsync(string companyCd, string customerCd, long? customerId = null, string? databaseName = null);
    }
}
