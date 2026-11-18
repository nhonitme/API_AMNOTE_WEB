using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Helpers
{
    public static class ChitNoteExcelHelper
    {
        /// <summary>
        /// Company voucher templates (tooltaomau) often omit the DETAIL_ prefix.
        /// These bare keys must map to chit detail — not header — or amount/accounts land on header only.
        /// </summary>
        private static readonly HashSet<string> BareChitDetailKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "AMOUNT",
            "DEBIT",
            "DEBIT_CD",
            "CREDIT",
            "CREDIT_CD",
            "CUSTOMER_CD",
            "CUSTOMER_OWN_CD",
            "BANK_CD",
            "BANK_OWN_CD",
            "DEPARTMENT_CD",
            "DEPARTMENT_CD_2",
            "MG_CD",
            "MG_CD_2",
            "MR_CD",
            "MR_CD2",
            "FC_TYPE",
            "FC_AMOUNT",
            "FC_RATE",
            "FC_DATETIME",
            "SORT",
            "CHIT_VMD",
            "HASINVENTORY",
            "INVENTORY_YMD",
            "ISPAY",
            "ISCOLLECT",
            "DETAIL_DESCRIPTION_VIET",
            "DETAIL_DESCRIPTION_ENG",
            "DETAIL_DESCRIPTION_KOR"
        };

        public static bool IsChitDetailTemplateKey(string key)
        {
            if (Common.IsDetailTemplateKey(key))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(key) && BareChitDetailKeys.Contains(key.Trim());
        }

        public static List<string> NormalizeImportErrors(IEnumerable<string> errors)
        {
            return (errors ?? Enumerable.Empty<string>())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => Regex.Replace(error, @"^\S+\s+(?=\d+:)", "Row "))
                .ToList();
        }

        public static async Task<(List<(TRequest Record, int Row)> Records, List<string> Errors)> BuildImportRecordsAsync<TRequest, TDetailRequest>(
            List<Dictionary<string, object>> importedData,
            IEnumerable<string> templateKeys,
            string supportedType,
            string lang)
            where TRequest : class, new()
            where TDetailRequest : class, new()
        {
            var keys = Common.NormalizeTemplateKeys(templateKeys);
            // Bare AMOUNT/DEBIT_CD/... (company templates) must go to detail — same as tooltaomau SQL import.
            var headerKeys = keys.Where(key => !IsChitDetailTemplateKey(key)).ToList();
            var detailKeys = keys.Where(IsChitDetailTemplateKey).ToList();
            var errors = new List<string>();
            var groupedRecords = new Dictionary<string, (TRequest Record, int Row)>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < importedData.Count; index++)
            {
                var row = importedData[index];
                var rowNumber = ExcelImportHandlerHelper.GetExcelRowNo(row, index);
                var chitNo = Common.GetStringValue(row, "CHIT_NO");
                // Excel số phiếu = CHIT_NO only. Do not group by CHIT_CD (legacy/system key).
                var groupKey = !string.IsNullOrWhiteSpace(chitNo)
                    ? $"NO:{chitNo.Trim()}"
                    : $"ROW:{rowNumber}";

                if (!groupedRecords.TryGetValue(groupKey, out var groupedRecord))
                {
                    var request = new TRequest();
                    SetPropertyValue(request, "CHIT_NO", chitNo);
                    SetPropertyValue(request, "CHIT_TYPE", supportedType);

                    groupedRecord = (request, rowNumber);
                    groupedRecords[groupKey] = groupedRecord;
                }

                MergeHeaderValues(groupedRecord.Record, row, headerKeys, supportedType);
                AddDetail(groupedRecord.Record, BuildDetailRecord<TDetailRequest>(row, detailKeys, GetDetailCount(groupedRecord.Record) + 1));
            }

            return (groupedRecords.Values.ToList(), errors);
        }

        private static void MergeHeaderValues(object request, IDictionary<string, object> row, IReadOnlyList<string> headerKeys, string supportedType)
        {
            SetPropertyValue(request, "CHIT_TYPE", supportedType);

            foreach (var key in headerKeys)
            {
                if (key.Equals("CHIT_TYPE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var property = request.GetType().GetProperty(key);
                if (property == null || !property.CanWrite)
                {
                    continue;
                }

                SetPropertyValueIfEmpty(request, property, GetCellValue(row, key, property.PropertyType));
            }
        }

        private static TDetailRequest BuildDetailRecord<TDetailRequest>(IDictionary<string, object> row, IReadOnlyList<string> detailKeys, int sortOrder)
            where TDetailRequest : class, new()
        {
            var detail = new TDetailRequest();

            foreach (var key in detailKeys)
            {
                var propertyName = GetDetailPropertyName(key);
                var property = detail.GetType().GetProperty(propertyName);
                if (property == null || !property.CanWrite)
                {
                    continue;
                }

                var value = propertyName.Equals("SORT", StringComparison.OrdinalIgnoreCase)
                    ? GetCellValue(row, key, property.PropertyType) ?? sortOrder
                    : GetCellValue(row, key, property.PropertyType);

                SetPropertyValue(detail, propertyName, value);
            }

            return detail;
        }

        private static int GetDetailCount(object request)
        {
            var details = request.GetType().GetProperty("DETAILS")?.GetValue(request) as ICollection;
            return details?.Count ?? 0;
        }

        private static void AddDetail(object request, object detail)
        {
            if (request.GetType().GetProperty("DETAILS")?.GetValue(request) is IList list)
            {
                list.Add(detail);
            }
        }

        private static string GetDetailPropertyName(string key)
        {
            if (key.StartsWith("DETAIL_DESCRIPTION_", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }

            // Bare DETAIL_DESCRIPTION_* aliases used by some company templates
            if (key.Equals("DETAIL_DESCRIPTION_VIET", StringComparison.OrdinalIgnoreCase)
                || key.Equals("DETAIL_DESCRIPTION_ENG", StringComparison.OrdinalIgnoreCase)
                || key.Equals("DETAIL_DESCRIPTION_KOR", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }

            var propertyName = key.StartsWith("DETAIL_", StringComparison.OrdinalIgnoreCase)
                ? key.Substring("DETAIL_".Length)
                : key;

            return propertyName.ToUpperInvariant() switch
            {
                "DEBIT_CD" => "DEBIT",
                "CREDIT_CD" => "CREDIT",
                _ => propertyName
            };
        }

        private static object? GetCellValue(IDictionary<string, object> row, string key, Type targetType)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var actualType = nullableType ?? targetType;

            if (actualType == typeof(string))
            {
                if (key.EndsWith("_YMD", StringComparison.OrdinalIgnoreCase) ||
                    key.EndsWith("_VMD", StringComparison.OrdinalIgnoreCase))
                {
                    return Common.GetYmdStringValue(row, key);
                }

                return Common.GetStringValue(row, key);
            }

            if (actualType == typeof(DateTime))
            {
                return Common.GetDateValue(row, key);
            }

            if (actualType == typeof(int))
            {
                return Common.GetNullableIntValue(row, key);
            }

            if (actualType == typeof(long))
            {
                return Common.GetLongValue(row, key);
            }

            if (actualType == typeof(decimal))
            {
                return Common.GetNullableDecimalValue(row, key);
            }

            if (actualType == typeof(bool))
            {
                var text = Common.GetStringValue(row, key);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (bool.TryParse(text, out var boolValue))
                {
                    return boolValue;
                }

                if (text == "1")
                {
                    return true;
                }

                if (text == "0")
                {
                    return false;
                }

                return null;
            }

            if (!row.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                return null;
            }

            try
            {
                return Convert.ChangeType(rawValue, actualType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return rawValue;
            }
        }

        private static void SetPropertyValue(object target, string propertyName, object? value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            property.SetValue(target, value);
        }

        private static void SetPropertyValueIfEmpty(object target, PropertyInfo property, object? nextValue)
        {
            if (nextValue == null)
            {
                return;
            }

            var currentValue = property.GetValue(target);
            if (property.PropertyType == typeof(string))
            {
                if (string.IsNullOrWhiteSpace(currentValue as string) && !string.IsNullOrWhiteSpace(nextValue as string))
                {
                    property.SetValue(target, nextValue);
                }

                return;
            }

            if (currentValue == null)
            {
                property.SetValue(target, nextValue);
            }
        }
    }
}
