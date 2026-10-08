using API_AMNOTE_WEB.Controllers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using DevExpress.CodeParser;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Helpers
{
    public static class ImportColumnExplanationProviders
    {
        private static readonly IReadOnlyDictionary<string, Func<string, string, Task<IEnumerable<string>>>> Providers =
            new Dictionary<string, Func<string, string, Task<IEnumerable<string>>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ACC_CD"] = GetAccountCdExplanationAsync,
                ["DEBIT"] = GetAccountCdExplanationAsync,
                ["DEBIT_CD"] = GetAccountCdExplanationAsync,
                ["CREDIT"] = GetAccountCdExplanationAsync,
                ["CREDIT_CD"] = GetAccountCdExplanationAsync,
                ["DESTINATION_ACC_CD"] = GetAccountCdExplanationAsync,
                ["BANK_CD"] = GetBankCdExplanationAsync,
                ["CUSTOMER_CD"] = GetCustomerCdExplanationAsync,
                ["DEPARTMENT_CD"] = GetDepartmentCdExplanationAsync,
                ["DEPARTMENT_CD_2"] = GetDepartmentCdExplanationAsync,
                ["MG_CD"] = GetManagementCdExplanationAsync,
                ["MG_CD_2"] = GetManagementCdExplanationAsync,
                ["MG_CD_ROOT"] = GetManagementCdExplanationAsync,
                ["MR_CD"] = GetManagementCdExplanationAsync,
                ["MR_CD2"] = GetManagementCdExplanationAsync,
                ["PRODUCT_CD"] = GetProductCdExplanationAsync,
                ["PRODUCT_KIND_CD"] = GetProductKindCdExplanationAsync,
                ["STORE_CD"] = GetStoreCdExplanationAsync,
                ["STORE_KIND_CD"] = GetStoreKindCdExplanationAsync,
                ["UNIT_CD"] = GetProductUnitCdExplanationAsync
            };


        public static Task<IEnumerable<EtcInfo>> GetImportColumnExplanationAsync(string lang, string TABLE_NM, string COLUMN_NM, string CompanyCd)
        {
            //var explain_table_query = $"SELECT {COLUMN_NM} AS CD, {COLUMN_NM} AS NM_VIET FROM {TABLE_NM} WHERE ROWNUM <= {ImportExplanationMaxRows}";
            var explain_table_query = $"SELECT {COLUMN_NM} AS CD, '' AS NM_VIET, '' AS NM_ENG, '' AS NM_KOR, '' AS NM_CHINA FROM {TABLE_NM} WHERE ifnull(ISDEL,'') <> '1' AND COMPANY_CD = '{CompanyCd}'; ";
            return GetImportColumnExplanationAsync(lang, explain_table_query, CompanyCd);
        }

        private const int ImportExplanationMaxRows = 50;        
        //Giải thích tải dữ liệu
        public static async Task<IEnumerable<EtcInfo>> GetImportColumnExplanationAsync( string lang, string explain_table_query, string CompanyCd
                                                                                        //string explain_table, string explain_column_cd, string explain_column_nm, string explain_where
                                                                                        )
        {

            //var p_param1 = "SELECT "+ explain_column_cd + ", "+ explain_column_nm + " FROM "+ explain_table; // query select
            //if (explain_where + "" != "") p_param1 += " WHERE " + explain_where;
            var p_param1 = explain_table_query;
            var p_param2 = "";// where 
            try
            {
                var scopeFactory = ResolveScopeFactory();
                using var scope = scopeFactory.CreateScope();
                var _systemService = scope.ServiceProvider.GetService<ISystemService>()
                    ?? throw new InvalidOperationException("ISystemService not registered in DI container");


                //

                //var companyCd = Common.GetCompanyCode();
                var normalizedLang = lang + "";
                var normalizedEtcType = "1";
                var normalizedParam1 = p_param1;
                var normalizedParam2 = p_param2;
                //
                var result = await _systemService.GetEtcInfoAsync(CompanyCd, normalizedEtcType, normalizedLang, normalizedParam1, normalizedParam2);
                if (result != null)
                    return result.ToList() ;
                //return result.ToList();
            }
            catch (UnauthorizedAccessException ex)
            {                
                //return Error(ApiStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {                
                //return Error(ApiStatusCode.InternalServerError, $"Error: {ex.Message}");
            }


            return null;
        }

        private static bool TryGetImportColumnExplanationProvider(IEnumerable<string> candidates, out Func<string, string, Task<IEnumerable<string>>> provider)
        {
            provider = null!;
            foreach (var candidate in candidates)
            {
                if (Providers.TryGetValue(candidate, out var matchedProvider))
                {
                    provider = matchedProvider;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> GetColumnKeyCandidates(string normalizedColumn)
        {
            yield return normalizedColumn;

            foreach (var prefix in new[] { "DETAIL_", "INPUT_", "OUTPUT_" })
            {
                if (normalizedColumn.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return normalizedColumn[prefix.Length..];
                    yield break;
                }
            }
        }

        private static async Task<IEnumerable<string>> GetAccountCdExplanationAsync(string companyCd, string lang)
        {
            var accounts = await GetLookupRecordsAsync<IAcclistInfoService, AcclistInfo>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(accounts, item => item.ACC_CD, item => GetLocalizedAccountName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetBankCdExplanationAsync(string companyCd, string lang)
        {
            var banks = await GetLookupRecordsAsync<IBankInfoService, BankInfo>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(banks, item => item.BANK_CD, item => item.BANK_NM);
        }

        private static async Task<IEnumerable<string>> GetCustomerCdExplanationAsync(string companyCd, string lang)
        {
            var customers = await GetLookupRecordsAsync<ICustomerInfoService, CustomerInfoCustomerExt>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(customers, item => item.CUSTOMER_CD, item => GetLocalizedCustomerName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetDepartmentCdExplanationAsync(string companyCd, string lang)
        {
            var departments = await GetLookupRecordsAsync<IDepartmentInfoService, DepartmentInfo>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(departments, item => item.DEPARTMENT_CD, item => GetLocalizedDepartmentName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetManagementCdExplanationAsync(string companyCd, string lang)
        {
            var managementItems = await GetLookupRecordsAsync<IManagementInfoService, ManagementInfo>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(managementItems, item => item.MG_CD, item => GetLocalizedManagementName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetProductCdExplanationAsync(string companyCd, string lang)
        {
            var products = await GetLookupRecordsAsync<IProductInfoService, ProductInfoDto>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(products, item => item.PRODUCT_CD, item => GetLocalizedProductName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetProductKindCdExplanationAsync(string companyCd, string lang)
        {
            var productKinds = await GetLookupRecordsAsync<IProductKindService, ProductKind>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(productKinds, item => item.PRODUCT_KIND_CD, item => GetLocalizedProductKindName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetProductUnitCdExplanationAsync(string companyCd, string lang)
        {
            var productUnits = await GetLookupRecordsAsync<IProductUnitService, ProductUnit>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(productUnits, item => item.UNIT_CD, item => item.UNIT_NM);
        }

        private static async Task<IEnumerable<string>> GetStoreCdExplanationAsync(string companyCd, string lang)
        {
            var stores = await GetLookupRecordsAsync<IStoreInfoService, StoreInfo>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(stores, item => item.STORE_CD, item => GetLocalizedStoreName(item, lang));
        }

        private static async Task<IEnumerable<string>> GetStoreKindCdExplanationAsync(string companyCd, string lang)
        {
            var storeKinds = await GetLookupRecordsAsync<IStoreKindInfoService, StoreKindInfo>(
                service => service.GetListAsync(companyCd));

            return BuildImportExplanationLines(storeKinds, item => item.STORE_KIND_CD, item => GetLocalizedStoreKindName(item, lang));
        }

        private static async Task<IEnumerable<TRecord>> GetLookupRecordsAsync<TService, TRecord>(
            Func<TService, Task<IEnumerable<TRecord>>> query)
            where TService : class
        {
            using var scope = ResolveScopeFactory().CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<TService>();
            var records = await query(service);
            return records?.ToList() ?? Enumerable.Empty<TRecord>();
        }

        private static string GetLocalizedProductName(ProductInfoDto item, string lang)
        {
            return GetLocalizedMasterName(item.PRODUCT_NM_VIET, item.PRODUCT_NM_ENG, item.PRODUCT_NM_KOR, item.PRODUCT_NM_CHINA, item.PRODUCT_CD, lang);
        }

        private static string GetLocalizedCustomerName(CustomerInfoCustomerExt item, string lang)
        {
            return GetLocalizedMasterName(item.CUSTOMER_NM_VIET, item.CUSTOMER_NM_ENG, item.CUSTOMER_NM_KOR, item.CUSTOMER_NM_CHINA, item.CUSTOMER_CD, lang);
        }

        private static string GetLocalizedProductKindName(ProductKind item, string lang)
        {
            return GetLocalizedMasterName(item.PRODUCTKIND_NM_VIET, item.PRODUCTKIND_NM_ENG, item.PRODUCTKIND_NM_KOR, item.PRODUCTKIND_NM_CHINA, item.PRODUCT_KIND_CD, lang);
        }

        private static string GetLocalizedStoreName(StoreInfo item, string lang)
        {
            return GetLocalizedMasterName(item.STORE_NM_VIET, item.STORE_NM_ENG, item.STORE_NM_KOR, item.STORE_NM_CHINA, item.STORE_CD, lang);
        }

        private static string GetLocalizedStoreKindName(StoreKindInfo item, string lang)
        {
            return GetLocalizedMasterName(item.STORE_KIND_NM_VIET, item.STORE_KIND_NM_ENG, item.STORE_KIND_NM_KOR, item.STORE_KIND_NM_CHINA, item.STORE_KIND_CD, lang);
        }

        private static string GetLocalizedDepartmentName(DepartmentInfo item, string lang)
        {
            return GetLocalizedMasterName(item.DEP_NAME_VIET, item.DEP_NAME_ENG, item.DEP_NAME_KOR, item.DEP_NAME_CHINA, item.DEPARTMENT_CD, lang);
        }

        private static string GetLocalizedAccountName(AcclistInfo item, string lang)
        {
            return GetLocalizedMasterName(item.ACCTITLE_NM_VIET, item.ACCTITLE_NM_ENG, item.ACCTITLE_NM_KOR, item.ACCTITLE_NM_CHINA, item.ACC_CD, lang);
        }

        private static string GetLocalizedManagementName(ManagementInfo item, string lang)
        {
            return GetLocalizedMasterName(item.MG_DESC_VIET, item.MG_DESC_ENG, item.MG_DESC_KOR, null, item.MG_CD, lang);
        }

        private static string GetLocalizedMasterName(string? viet, string? eng, string? kor, string? china, string fallback, string lang)
        {
            return Common.NormalizeLanguageCode(lang) switch
            {
                "ENG" => FirstText(eng, viet, fallback),
                "KOR" => FirstText(kor, viet, fallback),
                "CHN" or "THA" => FirstText(china, viet, fallback),
                _ => FirstText(viet, fallback)
            };
        }

        private static string FirstText(params string?[] values)
        {
            foreach (var value in values)
            {
                var normalizedValue = Common.NormalizeNullableText(value);
                if (normalizedValue != null)
                {
                    return normalizedValue;
                }
            }

            return string.Empty;
        }

        private static List<string> BuildImportExplanationLines<T>(
            IEnumerable<T> items,
            Func<T, string?> codeSelector,
            Func<T, string?> nameSelector)
        {
            var lines = new List<string>();

            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                var code = Common.NormalizeNullableText(codeSelector(item));
                if (code == null)
                {
                    continue;
                }

                var name = Common.NormalizeNullableText(nameSelector(item));
                lines.Add(name == null ? code : $"{code} - {name}");
                if (lines.Count >= ImportExplanationMaxRows)
                {
                    break;
                }
            }

            return lines;
        }

        private static IServiceScopeFactory ResolveScopeFactory()
        {
            if (Common.ServiceProvider == null)
                throw new InvalidOperationException("Common.ServiceProvider is not initialized. Set it in Program.cs after building the app: Common.ServiceProvider = app.Services;");

            return Common.ServiceProvider.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory
                ?? throw new InvalidOperationException("IServiceScopeFactory not available in DI container");
        }
    }
}