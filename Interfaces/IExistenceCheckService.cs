using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IExistenceCheckService
    {
        Task<bool> ExistsAsync(
            string tableName,
            string codeField,
            string codeValue,
            string? companyCd,
            string? companyField = "COMPANY_CD",
            string? idField = null,
            long? excludeId = null,
            bool excludeDeleted = true,
            string deleteFlagField = "ISDEL",
            Net_DB db = Net_DB.Net_DB_Company,
            string? dbName = null);
    }
}
