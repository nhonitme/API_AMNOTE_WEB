using API_AMNOTE_WEB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Helpers
{
    public static class BuildImportRecords
    {
        public static async Task<(List<(CustomerInfoCustomerExtRequest Record, int Row)> Records, List<string> Errors)> BuildCustomerImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang)
        {
            var errors = new List<string>();
            var records = new List<(CustomerInfoCustomerExtRequest Record, int Row)>();

            var requiredMessage = (await Common.getLanguage("CUSTOMER_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var customerCd = Common.GetStringValue(row, "CUSTOMER_CD");
                if (string.IsNullOrWhiteSpace(customerCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredMessage}");
                    rowNumber++;
                    continue;
                }

                var customerName = Common.GetStringValue(row, "CUSTOMER_NM_VIET");

                records.Add((new CustomerInfoCustomerExtRequest
                {
                    CUSTOMER_CD = customerCd,
                    CATEGORY_CD = Common.GetStringValue(row, "CATEGORY_CD"),
                    CUSTOMER_TYPE = Common.GetStringValue(row, "CUSTOMER_TYPE"),
                    CUSTOMER_NM_VIET = string.IsNullOrWhiteSpace(customerName) ? null : customerName,
                    CUSTOMER_NM_ENG = Common.GetStringValue(row, "CUSTOMER_NM_ENG"),
                    CUSTOMER_NM_KOR = Common.GetStringValue(row, "CUSTOMER_NM_KOR"),
                    CUSTOMER_NM_CHINA = Common.GetStringValue(row, "CUSTOMER_NM_CHINA"),
                    ADDRESS = Common.GetStringValue(row, "ADDRESS"),
                    TEL = Common.GetStringValue(row, "TEL"),
                    ISDEL = Common.GetStringValue(row, "ISDEL"),
                    FAX = Common.GetStringValue(row, "FAX"),
                    TAX_CD = Common.GetStringValue(row, "TAX_CD"),
                    BANK_CD = Common.GetStringValue(row, "BANK_CD"),
                    EMAIL = Common.GetStringValue(row, "EMAIL"),
                    NOTE = Common.GetStringValue(row, "NOTE"),
                    IDNUMBER = Common.GetStringValue(row, "IDNUMBER"),
                    BUYER_NM = Common.GetStringValue(row, "BUYER_NM")
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<(List<(BankInfoRequest Record, int Row)> Records, List<string> Errors)> BuildBankImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang)
        {
            var errors = new List<string>();
            var records = new List<(BankInfoRequest Record, int Row)>();

            var requiredBankCode = (await Common.getLanguage("BANK_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));
            var requiredBankName = (await Common.getLanguage("BANK_NM", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var bankCd = Common.GetStringValue(row, "BANK_CD");
                if (string.IsNullOrWhiteSpace(bankCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredBankCode}");
                    rowNumber++;
                    continue;
                }

                var bankName = Common.GetStringValue(row, "BANK_NM");
                if (string.IsNullOrWhiteSpace(bankName))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredBankName}");
                    rowNumber++;
                    continue;
                }

                records.Add((new BankInfoRequest
                {
                    BANK_CD = bankCd,
                    BANK_NM = bankName,
                    ACC_CD = Common.GetStringValue(row, "ACC_CD"),
                    PASSBOOK_NM = Common.GetStringValue(row, "PASSBOOK_NM"),
                    ACCOUNT_NUM = Common.GetStringValue(row, "ACCOUNT_NUM"),
                    CITAD_CODE = Common.GetStringValue(row, "CITAD_CODE"),
                    REMARK = Common.GetStringValue(row, "REMARK")
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<List<string>> ValidateDuplicateBankImportAsync(string lang, List<(BankInfoRequest Record, int Row)> records, Func<string, Task<bool>> existsAsync)
        {
            var errors = new List<string>();
            var duplicateMessage = (await Common.getLanguage("BANK_CD", lang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", lang));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (record, row) in records)
            {
                var code = record.BANK_CD?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                if (!seen.Add(code))
                {
                    errors.Add($"Dòng {row}: {duplicateMessage}");
                }
            }

            var uniqueCodes = records
                .Select(r => r.Record.BANK_CD?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var code in uniqueCodes)
            {
                if (await existsAsync(code!))
                {
                    var rows = records
                        .Where(x => string.Equals(x.Record.BANK_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Row);

                    foreach (var row in rows)
                    {
                        errors.Add($"Dòng {row}: {duplicateMessage}");
                    }
                }
            }

            return errors;
        }

        public static async Task<List<string>> ValidateDuplicateCustomerImportAsync(string lang, List<(CustomerInfoCustomerExtRequest Record, int Row)> records, Func<string, Task<bool>> existsAsync)
        {
            var errors = new List<string>();
            var duplicateMessage = (await Common.getLanguage("CUSTOMER_CD", lang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", lang));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (record, row) in records)
            {
                var code = record.CUSTOMER_CD?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                if (!seen.Add(code))
                {
                    errors.Add($"Dòng {row}: {duplicateMessage}");
                }
            }

            var uniqueCodes = records
                .Select(r => r.Record.CUSTOMER_CD?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var code in uniqueCodes)
            {
                if (await existsAsync(code!))
                {
                    var rows = records
                        .Where(x => string.Equals(x.Record.CUSTOMER_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Row);

                    foreach (var row in rows)
                    {
                        errors.Add($"Dòng {row}: {duplicateMessage}");
                    }
                }
            }

            return errors;
        }

        public static async Task<(List<(StoreInfoRequest Record, int Row)> Records, List<string> Errors)> BuildStoreImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang, Dictionary<string, int> storeKindMap)
        {
            var errors = new List<string>();
            var records = new List<(StoreInfoRequest Record, int Row)>();

            var requiredStoreCd = (await Common.getLanguage("STORE_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));
            var requiredStoreName = (await Common.getLanguage("STORE_NM_VIET", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));
            var invalidKind = (await Common.getLanguage("STORE_KIND_CD", lang)) + " " + (await Common.getLanguage("INVALID", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var storeCd = Common.GetStringValue(row, "STORE_CD");
                if (string.IsNullOrWhiteSpace(storeCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredStoreCd}");
                    rowNumber++;
                    continue;
                }

                var storeName = Common.GetStringValue(row, "STORE_NM_VIET");
                if (string.IsNullOrWhiteSpace(storeName))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredStoreName}");
                    rowNumber++;
                    continue;
                }

                var storeKindCd = Common.GetStringValue(row, "STORE_KIND_CD");
                int storeKindId = 0;
                if (string.IsNullOrWhiteSpace(storeKindCd))
                {
                    errors.Add($"Dòng {rowNumber}: {invalidKind}");
                }
                else if (!storeKindMap.TryGetValue(storeKindCd.Trim(), out storeKindId))
                {
                    errors.Add($"Dòng {rowNumber}: {invalidKind}");
                }

                records.Add((new StoreInfoRequest
                {
                    STORE_CD = storeCd,
                    STORE_NM_VIET = storeName,
                    STORE_NM_ENG = Common.GetStringValue(row, "STORE_NM_ENG"),
                    STORE_NM_KOR = Common.GetStringValue(row, "STORE_NM_KOR"),
                    STORE_NM_CHINA = Common.GetStringValue(row, "STORE_NM_CHINA"),
                    STORE_KIND_ID = storeKindId
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<(List<(StoreKindInfoRequest Record, int Row)> Records, List<string> Errors)> BuildStoreKindImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang)
        {
            var errors = new List<string>();
            var records = new List<(StoreKindInfoRequest Record, int Row)>();

            var requiredStoreKindCd = (await Common.getLanguage("STORE_KIND_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));
            var requiredStoreKindName = (await Common.getLanguage("STORE_KIND_NM_VIET", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var storeKindCd = Common.GetStringValue(row, "STORE_KIND_CD");
                if (string.IsNullOrWhiteSpace(storeKindCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredStoreKindCd}");
                    rowNumber++;
                    continue;
                }

                var storeName = Common.GetStringValue(row, "STORE_KIND_NM_VIET");
                if (string.IsNullOrWhiteSpace(storeName))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredStoreKindName}");
                    rowNumber++;
                    continue;
                }

                records.Add((new StoreKindInfoRequest
                {
                    STORE_KIND_CD = storeKindCd,
                    STORE_KIND_NM_VIET = storeName,
                    STORE_KIND_NM_ENG = Common.GetStringValue(row, "STORE_KIND_NM_ENG"),
                    STORE_KIND_NM_KOR = Common.GetStringValue(row, "STORE_KIND_NM_KOR"),
                    STORE_KIND_NM_CHINA = Common.GetStringValue(row, "STORE_KIND_NM_CHINA"),
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<(List<(AcclistInfoRequest Record, int Row)> Records, List<string> Errors)> BuildAcclistImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang, Dictionary<string, string> acclistMap)
        {
            var errors = new List<string>();
            var records = new List<(AcclistInfoRequest Record, int Row)>();

            var requiredAccCd = (await Common.getLanguage("ACC_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));
            var requiredAccName = (await Common.getLanguage("ACCTITLE_NM_VIET", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var accCd = Common.GetStringValue(row, "ACC_CD");
                if (string.IsNullOrWhiteSpace(accCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredAccCd}");
                    rowNumber++;
                    continue;
                }

                var accName = Common.GetStringValue(row, "ACCTITLE_NM_VIET");
                if (string.IsNullOrWhiteSpace(accName))
                {
                    errors.Add($"Dòng {rowNumber}: {accName}");
                    rowNumber++;
                    continue;
                }

                records.Add((new AcclistInfoRequest
                {
                    ACC_CD = accCd,
                    ACC_PARENT_ID = Common.GetIntValue(row, "ACC_PARENT_ID"),
                    ACCTITLE_NM_VIET = Common.GetStringValue(row, "ACCTITLE_NM_VIET"),
                    ACCTITLE_NM_ENG = Common.GetStringValue(row, "ACCTITLE_NM_ENG"),
                    ACCTITLE_NM_KOR = Common.GetStringValue(row, "ACCTITLE_NM_KOR"),
                    ACCTITLE_NM_CHINA = Common.GetStringValue(row, "ACCTITLE_NM_CHINA"),
                    ISABLETYPE = 1,
                    ISABLEINPUT = "1",
                    ISUSERADD = "1",
                    LEVEL = 0,
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<(List<(ManagementInfoRequest Record, int Row)> Records, List<string> Errors)> BuildManagementInfoImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang)
        {
            var errors = new List<string>();
            var records = new List<(ManagementInfoRequest Record, int Row)>();

            var requiredMessage = (await Common.getLanguage("MG_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var mgCd = Common.GetStringValue(row, "MG_CD");
                if (string.IsNullOrWhiteSpace(mgCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredMessage}");
                    rowNumber++;
                    continue;
                }

                records.Add((new ManagementInfoRequest
                {
                    MG_CD = mgCd,
                    MG_DESC_KOR = Common.GetStringValue(row, "MG_DESC_KOR"),
                    MG_DESC_ENG = Common.GetStringValue(row, "MG_DESC_ENG"),
                    MG_DESC_VIET = Common.GetStringValue(row, "MG_DESC_VIET"),
                    MG_CD_ROOT = Common.GetStringValue(row, "MG_CD_ROOT")
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<(List<(DepartmentInfoRequest Record, int Row)> Records, List<string> Errors)> BuildDepartmentImportRecordsAsync(List<Dictionary<string, object>> importedData, string lang)
        {
            var errors = new List<string>();
            var records = new List<(DepartmentInfoRequest Record, int Row)>();

            var requiredDepartmentCd = (await Common.getLanguage("DEPARTMENT_CD", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));
            var requiredDepartmentName = (await Common.getLanguage("DEP_NAME_VIET", lang)) + " " + (await Common.getLanguage("REQUIRED", lang));

            var rowNumber = 2;
            foreach (var row in importedData)
            {
                var departmentCd = Common.GetStringValue(row, "DEPARTMENT_CD");
                if (string.IsNullOrWhiteSpace(departmentCd))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredDepartmentCd}");
                    rowNumber++;
                    continue;
                }

                var departmentName = Common.GetStringValue(row, "DEP_NAME_VIET");
                if (string.IsNullOrWhiteSpace(departmentName))
                {
                    errors.Add($"Dòng {rowNumber}: {requiredDepartmentName}");
                    rowNumber++;
                    continue;
                }

                records.Add((new DepartmentInfoRequest
                {
                    DEPARTMENT_ID = 0,
                    DEPARTMENT_CD = departmentCd,
                    PARENT_CD = Common.GetStringValue(row, "PARENT_CD"),
                    DEP_NAME_KOR = Common.GetStringValue(row, "DEP_NAME_KOR"),
                    DEP_NAME_ENG = Common.GetStringValue(row, "DEP_NAME_ENG"),
                    DEP_NAME_VIET = departmentName,
                    DEP_NAME_CHINA = Common.GetStringValue(row, "DEP_NAME_CHINA"),
                    ISDEL = Common.GetStringValue(row, "ISDEL")
                }, rowNumber));

                rowNumber++;
            }

            return (records, errors);
        }

        public static async Task<List<string>> ValidateDuplicateDepartmentImportAsync(string lang, List<(DepartmentInfoRequest Record, int Row)> records, Func<string, Task<bool>> existsAsync)
        {
            var errors = new List<string>();
            var duplicateMessage = (await Common.getLanguage("DEPARTMENT_CD", lang)) + " " + (await Common.getLanguage("exists_Data", lang));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (record, row) in records)
            {
                var code = record.DEPARTMENT_CD?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (!seen.Add(code))
                {
                    errors.Add($"Dòng {row}: {duplicateMessage}");
                }
            }

            var uniqueCodes = records
                .Select(item => item.Record.DEPARTMENT_CD?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var code in uniqueCodes)
            {
                if (await existsAsync(code!))
                {
                    var rows = records
                        .Where(item => string.Equals(item.Record.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Row);

                    foreach (var row in rows)
                    {
                        errors.Add($"Dòng {row}: {duplicateMessage}");
                    }
                }
            }

            return errors;
        }

        public static async Task<List<string>> ValidateDuplicateStoreImportAsync(string lang, List<(StoreInfoRequest Record, int Row)> records, Func<string, Task<bool>> existsAsync)
        {
            var errors = new List<string>();
            var duplicateMessage = (await Common.getLanguage("STORE_CD", lang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", lang));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (record, row) in records)
            {
                var code = record.STORE_CD?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (!seen.Add(code))
                {
                    errors.Add($"DÃ²ng {row}: {duplicateMessage}");
                }
            }

            var uniqueCodes = records
                .Select(item => item.Record.STORE_CD?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var code in uniqueCodes)
            {
                if (await existsAsync(code!))
                {
                    var rows = records
                        .Where(item => string.Equals(item.Record.STORE_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Row);

                    foreach (var row in rows)
                    {
                        errors.Add($"DÃ²ng {row}: {duplicateMessage}");
                    }
                }
            }

            return errors;
        }

        public static async Task<List<string>> ValidateDuplicateStoreKindImportAsync(string lang, List<(StoreKindInfoRequest Record, int Row)> records, Func<string, Task<bool>> existsAsync)
        {
            var errors = new List<string>();
            var duplicateMessage = (await Common.getLanguage("STORE_KIND_CD", lang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", lang));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (record, row) in records)
            {
                var code = record.STORE_KIND_CD?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (!seen.Add(code))
                {
                    errors.Add($"DÃ²ng {row}: {duplicateMessage}");
                }
            }

            var uniqueCodes = records
                .Select(item => item.Record.STORE_KIND_CD?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var code in uniqueCodes)
            {
                if (await existsAsync(code!))
                {
                    var rows = records
                        .Where(item => string.Equals(item.Record.STORE_KIND_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Row);

                    foreach (var row in rows)
                    {
                        errors.Add($"DÃ²ng {row}: {duplicateMessage}");
                    }
                }
            }

            return errors;
        }

        public static async Task<List<string>> ValidateDuplicateManagementImportAsync(string lang, List<(ManagementInfoRequest Record, int Row)> records, Func<string, Task<bool>> existsAsync)
        {
            var errors = new List<string>();
            var duplicateMessage = (await Common.getLanguage("MG_CD", lang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", lang));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (record, row) in records)
            {
                var code = record.MG_CD?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (!seen.Add(code))
                {
                    errors.Add($"DÃ²ng {row}: {duplicateMessage}");
                }
            }

            var uniqueCodes = records
                .Select(item => item.Record.MG_CD?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var code in uniqueCodes)
            {
                if (await existsAsync(code!))
                {
                    var rows = records
                        .Where(item => string.Equals(item.Record.MG_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Row);

                    foreach (var row in rows)
                    {
                        errors.Add($"DÃ²ng {row}: {duplicateMessage}");
                    }
                }
            }

            return errors;
        }
    }
}
