using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed class ExcelImportLookupRepository : IExcelImportLookupRepository
    {
        private readonly DapperExecutor _db;

        public ExcelImportLookupRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<AcclistInfo>> GetAccountsAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getAccListInfo(@p_COMPANY_CD, @p_ACC_ID)";
            var rows = await _db.QueryAsync<AcclistInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_ACC_ID = (int?)null
            }, databaseName);

            return rows.ToList();
        }

        public async Task<IReadOnlyDictionary<string, long>> GetAccountIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            var rows = await GetAccountsAsync(companyCd, databaseName);
            return LookupMapBuilder.Build(rows, item => item.ACC_CD, item => (long)item.ACC_ID);
        }

        public async Task<IReadOnlyDictionary<string, BankInfo>> GetBanksByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getBankInfo(@p_COMPANY_CD, @p_BANK_ID, @p_BANK_CD)";
            var rows = await _db.QueryAsync<BankInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_BANK_ID = (long?)null,
                p_BANK_CD = (string?)null
            }, databaseName);

            var result = new Dictionary<string, BankInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var code = Common.NormalizeNullableText(row.BANK_CD);
                if (code == null || row.BANK_ID <= 0 || result.ContainsKey(code))
                {
                    continue;
                }

                result[code] = row;
            }

            return result;
        }

        public async Task<IReadOnlyDictionary<string, long>> GetBankIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            var banks = await GetBanksByCodeAsync(companyCd, databaseName);
            return banks.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.BANK_ID,
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetCustomerIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getCustomerInfoCustomerExt(@p_COMPANY_CD, @p_CUSTOMER_ID, @p_CUSTOMER_CD)";
            var rows = await _db.QueryAsync<CustomerInfoCustomerExt>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_CUSTOMER_ID = (long?)null,
                p_CUSTOMER_CD = (string?)null
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.CUSTOMER_CD, item => item.CUSTOMER_ID);
        }

        public async Task<IReadOnlyDictionary<string, DepartmentInfo>> GetDepartmentsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getDepartmentInfo(@p_COMPANY_CD, @p_DEPARTMENT_ID, @p_DEPARTMENT_CD)";
            var rows = await _db.QueryAsync<DepartmentInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_DEPARTMENT_ID = (long?)null,
                p_DEPARTMENT_CD = (string?)null
            }, databaseName);

            var result = new Dictionary<string, DepartmentInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var code = Common.NormalizeNullableText(row.DEPARTMENT_CD);
                if (code == null || row.DEPARTMENT_ID <= 0 || result.ContainsKey(code))
                {
                    continue;
                }

                result[code] = row;
            }

            return result;
        }

        public async Task<IReadOnlyDictionary<string, long>> GetDepartmentIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            var departments = await GetDepartmentsByCodeAsync(companyCd, databaseName);
            return departments.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.DEPARTMENT_ID,
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetManagementIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getManagementInfo(@p_COMPANY_CD, @p_MG_ID, @p_MG_CD)";
            var rows = await _db.QueryAsync<ManagementInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MG_ID = (long?)null,
                p_MG_CD = (string?)null
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.MG_CD, item => item.MG_ID);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetProductIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getProductInfo(@p_COMPANY_CD, @p_PRODUCT_ID)";
            var rows = await _db.QueryAsync<ProductInfoDto>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_ID = 0
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.PRODUCT_CD, item => (long)item.PRODUCT_ID);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetProductKindIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getProductKind(@p_COMPANY_CD, @p_PRODUCT_KIND_ID, @p_PRODUCT_KIND_CD)";
            var rows = await _db.QueryAsync<ProductKind>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_KIND_ID = 0,
                p_PRODUCT_KIND_CD = string.Empty
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.PRODUCT_KIND_CD, item => (long)item.PRODUCT_KIND_ID);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetProductUnitIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getProductUnit(@p_COMPANY_CD, @p_UNIT_ID, @p_UNIT_CD)";
            var rows = await _db.QueryAsync<ProductUnit>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_UNIT_ID = 0,
                p_UNIT_CD = string.Empty
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.UNIT_CD, item => (long)item.UNIT_ID);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetStoreIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getStoreInfo(@p_COMPANY_CD, @p_STORE_ID)";
            var rows = await _db.QueryAsync<StoreInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_STORE_ID = (int?)null
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.STORE_CD, item => (long)item.STORE_ID);
        }

        public async Task<IReadOnlyDictionary<string, long>> GetStoreKindIdsByCodeAsync(string companyCd, string? databaseName = null)
        {
            const string query = "CALL getStoreKindInfo(@p_COMPANY_CD, @p_STORE_KIND_ID)";
            var rows = await _db.QueryAsync<StoreKindInfo>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_STORE_KIND_ID = (int?)null
            }, databaseName);

            return LookupMapBuilder.Build(rows, item => item.STORE_KIND_CD, item => (long)item.STORE_KIND_ID);
        }
    }
}
