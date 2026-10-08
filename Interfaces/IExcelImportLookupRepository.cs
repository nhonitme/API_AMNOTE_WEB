using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IExcelImportLookupRepository
    {
        Task<IReadOnlyList<AcclistInfo>> GetAccountsAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetAccountIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, BankInfo>> GetBanksByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetBankIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetCustomerIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, DepartmentInfo>> GetDepartmentsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetDepartmentIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetManagementIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetProductIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetProductKindIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetProductUnitIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetStoreIdsByCodeAsync(string companyCd, string? databaseName = null);
        Task<IReadOnlyDictionary<string, long>> GetStoreKindIdsByCodeAsync(string companyCd, string? databaseName = null);
    }
}
