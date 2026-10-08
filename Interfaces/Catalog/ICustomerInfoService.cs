using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface ICustomerInfoService
    {
        Task<IEnumerable<CustomerInfoCustomerExt>> GetListAsync(string companyCd, long? customerId = null, string? customerCd = null);
        Task<CustomerInfoCustomerExt?> GetByIdAsync(string companyCd, long customerId);
        Task<CustomerInfoCustomerExt> CreateAsync(string companyCd, string userId, CustomerInfoCustomerExtRequest request);
        Task<CustomerInfoCustomerExt> UpdateAsync(string companyCd, string userId, long customerId, CustomerInfoCustomerExtRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, List<long> customerIds);
        Task<bool> CodeExistsAsync(string companyCd, string customerCd, long? excludeId = null);
        Task<int> BulkInsertAsync(string companyCd, string userId, List<CustomerInfoCustomerExtRequest> records, string? databaseName = null, string? lang = null);
    }
}
