using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services.Lookup
{
    public interface ILookupService
    {
        Task<IEnumerable<BankLookupDto>> GetBanksAsync(string companyCd);
        Task<IEnumerable<CustomerLookupDto>> GetCustomersAsync(string companyCd);
        Task<IEnumerable<DepartmentLookupDto>> GetDepartmentsAsync(string companyCd);
        Task<IEnumerable<ManagementLookupDto>> GetManagementsAsync(string companyCd);
        Task<IEnumerable<StoreLookupDto>> GetStoresAsync(string companyCd);
        Task<IEnumerable<StoreKindLookupDto>> GetStoreKindsAsync(string companyCd);
        Task<IEnumerable<ProductLookupDto>> GetProductsAsync(string companyCd);
        Task<IEnumerable<ProductKindLookupDto>> GetProductKindsAsync(string companyCd);
        Task<IEnumerable<UnitLookupDto>> GetUnitsAsync(string companyCd);
        Task<IEnumerable<CountryLookupDto>> GetCountriesAsync();
        Task<bool> CheckExistsAsync(string type, string code, long? currentId, string companyCd);
    }
}
