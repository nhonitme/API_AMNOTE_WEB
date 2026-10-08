using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Dynamic;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Helpers
{
    public static partial class Common
    {
        private static readonly HashSet<string> MessageLanguageKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "VIET",
            "ENG",
            "KOR",
            "JPN",
            "THA",
            "CHN"
        };

        private static readonly HashSet<string> ImportTemplateSystemIdKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "ID",
            "CHIT_ID",
            "CHITDETAIL_ID",
            "INPUT_ID",
            "OUTPUT_ID",
            "CUSTOMER_EXT_ID",
            "USER_ID",
            "USER_PK_ID",
            "USER_DETAIL_ID",
            "MAPPING_ID",
            "OPTION_ID",
            "DB_GROUP_ID",
            "MENU_ID",
            "PARENT_ID",
            "ROW_ID"
        };

        private static readonly string[] MessageFallbackColumns = { "ENG", "VIET", "KOR", "CHN", "THA", "JPN" };

        public static string getManagerDBConnectStr(Int32 iTimeOut = 300)
        {
            string str = string.Empty;
            AriaSecurity.AriaProvider ariase = new AriaSecurity.AriaProvider();
            string serverAdress = GlobalData.getCurrentServerAdress();
            string dBConnectPort = GlobalData.getCurrentPort();
            string dBConnectID = GlobalData.getCurrentID();
            string dBConnectPW = GlobalData.getCurrentPW();
            string managerDB = ariase.DecryptFromString(GlobalData.managerDB).Replace("\0", "").Trim();

            str = $"server={serverAdress};database={managerDB};uid={dBConnectID};pwd={dBConnectPW};port={dBConnectPort};allow user variables=true;default command timeout={iTimeOut};SSL Mode=Required";
            return str;
        }

        public static string getCompanyDBConnectStr(string companyDB, Int32 iTimeOut = 300)
        {
            string str = string.Empty;
            AriaSecurity.AriaProvider ariase = new AriaSecurity.AriaProvider();
            string serverAdress = GlobalData.getCurrentServerAdress();
            string dBConnectPort = GlobalData.getCurrentPort();
            string dBConnectID = GlobalData.getCurrentID();
            string dBConnectPW = GlobalData.getCurrentPW();

            str = $"server={serverAdress};database={companyDB};uid={dBConnectID};pwd={dBConnectPW};port={dBConnectPort};allow user variables=true;default command timeout={iTimeOut};SSL Mode=Required";
            return str;
        }

        private static long _lastMs = 0;
        private static int _seq = 0;
        private static readonly object _lock = new();

        public static string GenerateKeyCd(string? prefix = "D")
        {
            var safePrefix = Regex.Replace(NormalizeNullableText(prefix) ?? "D", "[^a-zA-Z0-9]", string.Empty).ToUpperInvariant();
            if (safePrefix.Length == 0)
            {
                safePrefix = "D";
            }

            safePrefix = safePrefix[..1];

            lock (_lock)
            {
                var currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentMs == _lastMs)
                {
                    if (_seq >= 999999)
                    {
                        do
                        {
                            currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        }
                        while (currentMs <= _lastMs);

                        _lastMs = currentMs;
                        _seq = 0;
                    }
                    else
                    {
                        _seq++;
                    }
                }
                else
                {
                    _lastMs = currentMs;
                    _seq = 0;
                }

                return $"{safePrefix}{currentMs:D13}{_seq:D6}";
            }
        }

        public static string? GetStringValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return null;

            if (!row.TryGetValue(key, out var value) || value == null)
                return null;

            return value.ToString()?.Trim();
        }

        public static string? GetYmdStringValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return null;

            if (!row.TryGetValue(key, out var value) || value == null)
                return null;

            if (value is DateTime dateTime)
                return dateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            if (value is double oaDouble)
                return DateTime.FromOADate(oaDouble).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            if (value is float oaFloat)
                return DateTime.FromOADate(oaFloat).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            if (value is decimal oaDecimal)
                return DateTime.FromOADate(Convert.ToDouble(oaDecimal)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            if (value is int oaInt)
                return DateTime.FromOADate(oaInt).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            var text = value.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return ParseNullableDateTimeText(text)?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? text;
        }

        public static DateTime? GetDateValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return null;

            if (!row.TryGetValue(key, out var value) || value == null)
                return null;

            if (value is DateTime dt)
                return dt;

            if (value is double oaDouble)
                return DateTime.FromOADate(oaDouble);

            if (value is float oaFloat)
                return DateTime.FromOADate(oaFloat);

            if (value is decimal oaDecimal)
                return DateTime.FromOADate(Convert.ToDouble(oaDecimal));

            if (value is int oaInt)
                return DateTime.FromOADate(oaInt);

            if (value is long oaLong)
                return DateTime.FromOADate(oaLong);

            return ParseNullableDateTimeText(value.ToString());
        }

        public static int GetIntValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return 0;

            if (!row.TryGetValue(key, out var value) || value == null)
                return 0;

            if (value is int i)
                return i;

            if (int.TryParse(value.ToString(), out var parsed))
                return parsed;

            return 0;
        }

        public static int? GetNullableIntValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return null;

            if (!row.TryGetValue(key, out var value) || value == null)
                return null;

            if (value is int intValue)
                return intValue;

            if (value is long longValue)
                return Convert.ToInt32(longValue);

            if (value is double doubleValue)
                return Convert.ToInt32(doubleValue);

            if (value is decimal decimalValue)
                return Convert.ToInt32(decimalValue);

            if (int.TryParse(value.ToString(), out var parsed))
                return parsed;

            return null;
        }

        public static long? GetLongValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return null;

            if (!row.TryGetValue(key, out var value) || value == null)
                return null;

            if (value is long l)
                return l;

            if (value is int i)
                return i;

            if (long.TryParse(value.ToString(), out var parsed))
                return parsed;

            return null;
        }

        public static decimal? GetNullableDecimalValue(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrWhiteSpace(key))
                return null;

            if (!row.TryGetValue(key, out var value) || value == null)
                return null;

            if (value is decimal decimalValue)
                return decimalValue;

            if (value is double doubleValue)
                return Convert.ToDecimal(doubleValue);

            if (value is float floatValue)
                return Convert.ToDecimal(floatValue);

            if (value is int intValue)
                return intValue;

            if (value is long longValue)
                return longValue;

            if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
                return parsed;

            return null;
        }

        public static string NormalizeRequiredText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        public static string? NormalizeNullableText(string? value)
        {
            var normalized = NormalizeRequiredText(value);
            return normalized.Length == 0 ? null : normalized;
        }

        public static string NormalizeRequiredValue(string? value, string parameterName, int maxLength)
        {
            var normalized = NormalizeRequiredText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException($"{parameterName} is required");
            }

            if (normalized.Length > maxLength)
            {
                throw new ArgumentException($"{parameterName} length must be less than or equal to {maxLength}");
            }

            return normalized;
        }

        public static string? NormalizeTemplateId(string? templateId, bool required)
        {
            var normalized = NormalizeNullableText(templateId);
            if (normalized == null)
            {
                if (required)
                {
                    throw new ArgumentException("TEMPLATE_ID is required");
                }

                return null;
            }

            if (normalized.Length > 50)
            {
                throw new ArgumentException("TEMPLATE_ID length must be less than or equal to 50");
            }

            return normalized;
        }

        public static string NormalizeTemplateName(string? templateName, string templateId, string defaultTemplateId = "DEFAULT", string defaultTemplateName = "Default")
        {
            var normalized = NormalizeNullableText(templateName);
            if (normalized == null)
            {
                return templateId.Equals(defaultTemplateId, StringComparison.OrdinalIgnoreCase)
                    ? defaultTemplateName
                    : templateId;
            }

            return normalized.Length > 100 ? normalized[..100] : normalized;
        }

        public const int DefaultUserLevel = 40;

        public static readonly int[] ValidUserLevels = { 10, 20, 30, 40 };

        public static bool IsValidUserLevel(int userLevel) =>
            Array.IndexOf(ValidUserLevels, userLevel) >= 0;

        /// <summary>
        /// USERLV: smaller number = higher privilege (10 ADMIN … 40 VIEW).
        /// </summary>
        public static int MapLegacyUserLevel(int userLevel) =>
            userLevel switch
            {
                10 or 20 or 30 or 40 => userLevel,
                9 => 10,
                3 => 20,
                2 => 30,
                1 => 40,
                _ => DefaultUserLevel
            };

        public static int NormalizeUserLevel(int? requestedLevel, int? existingLevel = null)
        {
            if (requestedLevel.HasValue && requestedLevel.Value > 0)
            {
                return MapLegacyUserLevel(requestedLevel.Value);
            }

            if (existingLevel.HasValue && existingLevel.Value > 0)
            {
                return MapLegacyUserLevel(existingLevel.Value);
            }

            return DefaultUserLevel;
        }

        public static string DeriveRoleCode(int userLevel) =>
            NormalizeUserLevel(userLevel) switch
            {
                <= 10 => "ADMIN",
                <= 20 => "MANAGER",
                <= 30 => "STAFF",
                _ => "VIEW"
            };

        public static bool IsValidEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string? NormalizeInputType(string? value)
        {
            var normalized = NormalizeNullableText(value)?.ToUpperInvariant();
            return normalized is "AP" or "AR" or "INV" ? normalized : null;
        }

        public static string? NormalizeInputType(string? inputType, string? chitType)
        {
            var normalizedInputType = NormalizeInputType(inputType);
            if (normalizedInputType != null)
            {
                return normalizedInputType;
            }

            return NormalizeChitType(chitType) switch
            {
                "PO" or "PD" or "PR" or "IR" => "AP",
                "SO" or "SD" or "SR" or "IO" => "AR",
                "IA" => "INV",
                _ => null
            };
        }

        public static string? NormalizeChitType(string? value)
        {
            var normalized = NormalizeNullableText(value)?.ToUpperInvariant();
            return normalized switch
            {
                "PURCHASE_RETURN" or "PURCHASE_RETURN_VOUCHER" => "PR",
                "PURCHASE_DISCOUNT" or "PURCHASE_DISCOUNT_VOUCHER" => "PD",
                "SALES_DISCOUNT" or "SALES_DISCOUNT_VOUCHER" => "SD",
                "SALES_RETURN" or "SALES_RETURN_VOUCHER" => "SR",
                "PURCHASE_VOUCHER" or "PURCHASE" => "PO",
                "SALES_VOUCHER" or "SALES" => "SO",
                "INVENTORY_RECEIPT" or "INVENTORY_RECEIPT_VOUCHER" => "IR",
                "INVENTORY_ISSUE" or "INVENTORY_ISSUE_VOUCHER" => "IO",
                "INVENTORY_ADJUST" or "INVENTORY_ADJUSTMENT" or "INVENTORY_ADJUSTMENT_VOUCHER" => "IA",
                "TRANSFER" or "INVENTORY_TRANSFER" or "INVENTORY_TRANSFER_VOUCHER" => "IA",
                "PR" or "PD" or "SD" or "SR" or "PO" or "SO" or "IR" or "IO" or "IA" => normalized,
                _ => null
            };
        }

        public static string NormalizeInventoryChitType(string? value)
        {
            var normalized = NormalizeChitType(value);
            if (normalized is "IR" or "IO" or "IA")
            {
                return normalized;
            }

            throw new ArgumentException("CHIT_TYPE is invalid");
        }

        public static string? NormalizeReferenceChitType(string? value)
        {
            var normalized = NormalizeUpperText(value);
            return normalized == null ? null : NormalizeChitType(normalized) ?? normalized;
        }

        public static string? NormalizeInventorySourceType(string? value, string? sourceChitType = null)
        {
            var normalized = NormalizeUpperText(value);

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized switch
                {
                    "PO" or "PURCHASE" or "PURCHASE_VOUCHER" => "PURCHASE",
                    "PD" or "PURCHASE_DISCOUNT" or "PURCHASE_DISCOUNT_VOUCHER" => "PURCHASE_DISCOUNT",
                    "PR" or "PURCHASE_RETURN" or "PURCHASE_RETURN_VOUCHER" => "PURCHASE_RETURN",
                    "SO" or "SALES" or "SALES_VOUCHER" => "SALES",
                    "SD" or "SALES_DISCOUNT" or "SALES_DISCOUNT_VOUCHER" => "SALES_DISCOUNT",
                    "SR" or "SALES_RETURN" or "SALES_RETURN_VOUCHER" => "SALES_RETURN",
                    "MANUAL" => "MANUAL",
                    _ => normalized
                };
            }

            return NormalizeReferenceChitType(sourceChitType) switch
            {
                "PO" => "PURCHASE",
                "PD" => "PURCHASE_DISCOUNT",
                "PR" => "PURCHASE_RETURN",
                "SO" => "SALES",
                "SD" => "SALES_DISCOUNT",
                "SR" => "SALES_RETURN",
                _ => null
            };
        }

        public static bool UsesInventoryInput(string? chitType)
        {
            return NormalizeChitType(chitType) is "IR" or "IA";
        }

        public static bool UsesInventoryOutput(string? chitType)
        {
            return NormalizeChitType(chitType) is "IO" or "IA";
        }

        public static string? NormalizeUpperText(string? value)
        {
            return NormalizeNullableText(value)?.ToUpperInvariant();
        }

        public static DateTime? ParseNullableDateTimeText(string? value)
        {
            var normalized = NormalizeNullableText(value);
            if (normalized == null)
            {
                return null;
            }

            if (DateTime.TryParseExact(normalized, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                return exact;
            }

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariantDate))
            {
                return invariantDate;
            }

            if (DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.None, out var localDate))
            {
                return localDate;
            }

            return null;
        }

        public static DateTime NormalizeSequenceBaseDate(DateTime? baseDate)
        {
            return baseDate?.Date ?? DateTime.Today;
        }

        public static string? NormalizeNullableInventoryDateTimeText(string? value, string parameterName)
        {
            var normalized = NormalizeNullableText(value);
            if (normalized == null)
            {
                return null;
            }

            var compact = normalized
                .Replace("-", string.Empty)
                .Replace("/", string.Empty)
                .Replace(".", string.Empty)
                .Replace(":", string.Empty)
                .Replace("T", string.Empty)
                .Replace(" ", string.Empty)
                .TrimEnd('Z');

            if (compact.All(char.IsDigit))
            {
                var exactFormats = compact.Length switch
                {
                    8 => new[] { "yyyyMMdd", "ddMMyyyy" },
                    12 => new[] { "yyyyMMddHHmm", "ddMMyyyyHHmm" },
                    14 => new[] { "yyyyMMddHHmmss", "ddMMyyyyHHmmss" },
                    _ => null
                };

                if (exactFormats != null && DateTime.TryParseExact(compact, exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDateTime))
                {
                    return exactDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                }
            }

            if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTimeOffset))
            {
                return dateTimeOffset.DateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTime)
                || DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dateTime))
            {
                return dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            }

            throw new ArgumentException($"{parameterName} must be a valid inventory date time");
        }

        public static DateTime? NormalizeNullableDate(string? value, string fieldName, string format = "yyyy-MM-dd")
        {
            var normalized = NormalizeNullableText(value);
            if (normalized == null)
            {
                return null;
            }

            if (DateTime.TryParseExact(normalized, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                return exact.Date;
            }

            var parsed = ParseNullableDateTimeText(normalized);
            if (parsed.HasValue)
            {
                return parsed.Value.Date;
            }

            throw new ArgumentException($"{fieldName} is invalid");
        }

        public static DateTime NormalizeRequiredDate(string? value, string fieldName, string format = "yyyy-MM-dd")
        {
            var parsed = NormalizeNullableDate(value, fieldName, format);
            if (parsed == null)
            {
                throw new ArgumentException($"{fieldName} is required");
            }

            return parsed.Value;
        }

        public static string NormalizeLanguageCode(string? lang)
        {
            var normalized = NormalizeNullableText(lang)?.ToUpperInvariant();
            return normalized != null && MessageLanguageKeys.Contains(normalized) ? normalized : "VIET";
        }

        public static string? NormalizeNullableYmdText(string? value, string parameterName = "YMD")
        {
            var normalized = NormalizeNullableText(value);
            if (normalized == null)
            {
                return null;
            }

            if (!DateTime.TryParseExact(normalized, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                throw new ArgumentException($"{parameterName} must be in yyyyMMdd format");
            }

            return normalized;
        }

        public static DateTime? ParseNullableYmdDate(string? value, string parameterName = "YMD")
        {
            var normalized = NormalizeNullableYmdText(value, parameterName);
            return normalized == null
                ? null
                : DateTime.ParseExact(normalized, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public static string? FormatNullableYmd(DateTime? value)
        {
            return value?.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        public static string NormalizeRateMethod(string? value, string defaultMethod = "WEIGHTED_AVERAGE")
        {
            var normalized = NormalizeUpperText(value) ?? defaultMethod;
            if (normalized == "1" || normalized == "AVG")
            {
                return defaultMethod;
            }

            if (normalized != defaultMethod)
            {
                throw new ArgumentException("RATE_METHOD must be WEIGHTED_AVERAGE");
            }

            return normalized;
        }

        public static void ValidateYmdRange(string? fromYmd, string? toYmd, string fromName = "CHIT_YMD_FROM", string toName = "CHIT_YMD_TO")
        {
            if (fromYmd != null && toYmd != null && string.CompareOrdinal(fromYmd, toYmd) > 0)
            {
                throw new ArgumentException($"{fromName} must be less than or equal to {toName}");
            }
        }

        public static (int pageNumber, int pageSize) ResolvePaging(long? singleId, int pageNumber, int pageSize)
            => singleId.HasValue ? (1, 1) : (pageNumber, pageSize);

        public static void ValidateDateRange(DateTime? fromDate, DateTime? toDate, string fromName = "RATE_DATE_FROM", string toName = "RATE_DATE_TO")
        {
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                throw new ArgumentException($"{fromName} must be less than or equal to {toName}");
            }
        }

        public static ExchangeRevaluationSummary BuildExchangeRevaluationSummary<T>(IEnumerable<T> rows, Func<T, string?> diffTypeSelector) where T : IExchangeRevaluationAmountInfo
        {
            var items = rows?.ToList() ?? new List<T>();
            return new ExchangeRevaluationSummary
            {
                TOTAL_ROWS = items.Count,
                TOTAL_OLD_AMOUNT = items.Sum(item => item.OLD_AMOUNT),
                TOTAL_NEW_AMOUNT = items.Sum(item => item.NEW_AMOUNT ?? 0),
                TOTAL_EXCHANGE_DIFF = items.Sum(item => item.EXCHANGE_DIFF ?? 0),
                TOTAL_GAIN = items.Where(item => string.Equals(diffTypeSelector(item), "GAIN", StringComparison.OrdinalIgnoreCase)).Sum(item => Math.Abs(item.EXCHANGE_DIFF ?? 0)),
                TOTAL_LOSS = items.Where(item => string.Equals(diffTypeSelector(item), "LOSS", StringComparison.OrdinalIgnoreCase)).Sum(item => Math.Abs(item.EXCHANGE_DIFF ?? 0))
            };
        }

        public static string NormalizeFlagString(string? value, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                ? "1"
                : "0";
        }

        public static int? NormalizeNullablePositiveInt(int? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value < 0 ? null : value.Value;
        }

        public static long? NormalizeNullablePositiveLong(long? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value > 0 ? value.Value : null;
        }

        public static List<long> ParsePositiveIds(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<long>();
            }

            return value
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Trim())
                .Where(item => long.TryParse(item, out _))
                .Select(long.Parse)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        public static string NormalizeToken(string? value) =>
            Regex.Replace(value ?? string.Empty, "[^a-zA-Z0-9]", string.Empty).ToLowerInvariant();

        public static string NormalizeSqlParameterName(string? key)
        {
            var value = NormalizeNullableText(key);
            if (value == null) return string.Empty;
            return value.StartsWith("p_", StringComparison.OrdinalIgnoreCase) ? value : $"p_{value}";
        }

        public static bool LooksLikeSqlCommand(string value) =>
            value.Contains(' ') || value.Contains('(') || value.Contains(')') ||
            value.Contains(';') || value.Contains('\n') || value.Contains('\r');

        public static (Net_DB Db, string CommandText) ParseCommandReference(string reference)
        {
            var value = reference.Trim();
            if (value.StartsWith("manager:", StringComparison.OrdinalIgnoreCase))
                return (Net_DB.Net_DB_Manager, value["manager:".Length..].Trim());
            if (value.StartsWith("company:", StringComparison.OrdinalIgnoreCase))
                return (Net_DB.Net_DB_Company, value["company:".Length..].Trim());
            return (Net_DB.Net_DB_Company, value);
        }

        public static Type? ResolveType(string typeName, Type? expectedBaseType = null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
            {
                var exactType = assembly.GetType(typeName, false, true);
                if (exactType != null && (expectedBaseType == null || expectedBaseType.IsAssignableFrom(exactType)))
                    return exactType;
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
            {
                IEnumerable<Type> types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null)!; }
                foreach (var type in types)
                {
                    if (!type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (expectedBaseType == null || expectedBaseType.IsAssignableFrom(type))
                        return type;
                }
            }
            return null;
        }

        public static List<string> NormalizeTemplateKeys(IEnumerable<string?> templateKeys)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in templateKeys ?? Enumerable.Empty<string?>())
            {
                var normalizedKey = NormalizeImportTemplateKey(key);
                if (string.IsNullOrWhiteSpace(normalizedKey) || !seen.Add(normalizedKey))
                {
                    continue;
                }

                result.Add(normalizedKey);
            }

            return result;
        }

        private static List<ExcelTemplateColumnInfo> NormalizeTemplateColumnInfos(IEnumerable<ExcelTemplateColumnInfo?> templateColumns)
        {
            var result = new List<ExcelTemplateColumnInfo>();
            var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in templateColumns ?? Enumerable.Empty<ExcelTemplateColumnInfo?>())
            {
                var normalizedKey = NormalizeImportTemplateKey(column?.FIELD_NAME);
                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    continue;
                }

                if (indexByKey.TryGetValue(normalizedKey, out var existingIndex))
                {
                    if (string.Equals(column?.IS_REQUIRED, "1", StringComparison.OrdinalIgnoreCase))
                    {
                        result[existingIndex].IS_REQUIRED = "1";
                    }

                    continue;
                }

                indexByKey[normalizedKey] = result.Count;
                result.Add(new ExcelTemplateColumnInfo
                {
                    FIELD_NAME = normalizedKey,
                    IS_REQUIRED = column?.IS_REQUIRED,
                    LABEL_TEXT = column?.LABEL_TEXT,
                    CAPTION = column?.CAPTION,
                    TABLE_NM = column?.TABLE_NM,
                    IS_PRIMARY_KEY = column?.IS_PRIMARY_KEY,
                    EXPLAIN_TABLE_QUERY = column?.EXPLAIN_TABLE_QUERY
                });
            }

            return result;
        }

        private static string? NormalizeImportTemplateKey(string? key)
        {
            var normalizedKey = NormalizeNullableText(key);
            if (normalizedKey == null)
            {
                return null;
            }

            var upperKey = normalizedKey.ToUpperInvariant();
            var baseKey = StripImportColumnPrefix(upperKey);
            if (ImportTemplateSystemIdKeys.Contains(baseKey))
            {
                return null;
            }

            return upperKey.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)
                ? $"{upperKey[..^3]}_CD"
                : upperKey;
        }

        private static string StripImportColumnPrefix(string key)
        {
            foreach (var prefix in new[] { "DETAIL_", "INPUT_", "OUTPUT_" })
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return key[prefix.Length..];
                }
            }

            return key;
        }

        public static bool IsDetailTemplateKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && key.StartsWith("DETAIL_", StringComparison.OrdinalIgnoreCase);
        }

        public static PropertyInfo? GetPublicProperty(Type type, string propertyName)
        {
            return type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        }

        public static object? GetPropertyValue(object? source, string propertyName)
        {
            return GetPublicProperty(source?.GetType() ?? typeof(object), propertyName)?.GetValue(source);
        }

        public static string? ResolveInventoryDetailPropertyName(string key, Type? detailType, bool explicitDetailKey)
        {
            if (detailType == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var candidate = explicitDetailKey
                ? StripInventoryDetailPrefix(key)
                : key.Trim();

            if (!explicitDetailKey && GetPublicProperty(typeof(InventoryVoucherRequest), candidate) != null)
            {
                return null;
            }

            candidate = ResolveInventoryDetailAlias(candidate);
            return GetPublicProperty(detailType, candidate) == null ? null : candidate;
        }

        private static string ResolveInventoryDetailAlias(string propertyName)
        {
            return propertyName.ToUpperInvariant() switch
            {
                "AMOUNT" => "AMOUNT_CC",
                "UNIT_PRICE" => "UNIT_PRICE_CC",
                "FC_AMOUNT" => "AMOUNT_FC",
                "FC_RATE" => "EXCHANGE_RATES",
                _ => propertyName
            };
        }

        private static string StripInventoryDetailPrefix(string key)
        {
            foreach (var prefix in new[] { "DETAIL_", "INPUT_", "OUTPUT_" })
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return key[prefix.Length..];
                }
            }

            return key.Trim();
        }

        public static async Task<object?> UnwrapAsyncResultAsync(object? result, Type returnType)
        {
            if (result == null) return null;
            if (returnType == typeof(Task)) { await (Task)result; return null; }
            if (typeof(Task).IsAssignableFrom(returnType))
            {
                await ((Task)result).ConfigureAwait(false);
                return returnType.GetProperty("Result")?.GetValue(result);
            }
            if (returnType == typeof(ValueTask)) { await ((ValueTask)result).ConfigureAwait(false); return null; }
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                dynamic valueTask = result;
                return await valueTask.ConfigureAwait(false);
            }
            return result;
        }

        public static object? ConvertRuntimeValue(object? rawValue, Type targetType)
        {
            if (rawValue == null)
                return Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType
                    ? null
                    : Activator.CreateInstance(targetType);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingType.IsInstanceOfType(rawValue)) return rawValue;
            if (rawValue is string text) return ConvertStringValue(text, underlyingType);
            if (underlyingType == typeof(string)) return rawValue.ToString();
            if (underlyingType == typeof(bool))
                return rawValue switch
                {
                    bool b => b,
                    int i => i != 0,
                    long l => l != 0,
                    _ => bool.Parse(rawValue.ToString() ?? string.Empty)
                };
            if (underlyingType.IsEnum)
                return rawValue is string s
                    ? Enum.Parse(underlyingType, s, true)
                    : Enum.ToObject(underlyingType, rawValue);
            return Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
        }

        private static object ConvertStringValue(string text, Type targetType)
        {
            var value = text.Trim();
            return targetType == typeof(string) ? value :
                targetType == typeof(int) ? int.Parse(value, CultureInfo.InvariantCulture) :
                targetType == typeof(long) ? long.Parse(value, CultureInfo.InvariantCulture) :
                targetType == typeof(short) ? short.Parse(value, CultureInfo.InvariantCulture) :
                targetType == typeof(decimal) ? decimal.Parse(value, CultureInfo.InvariantCulture) :
                targetType == typeof(double) ? double.Parse(value, CultureInfo.InvariantCulture) :
                targetType == typeof(float) ? float.Parse(value, CultureInfo.InvariantCulture) :
                targetType == typeof(bool) ? (object)(value == "1" || bool.Parse(value)) :
                targetType == typeof(DateTime) ? DateTime.Parse(value, CultureInfo.InvariantCulture) :
                targetType.IsEnum ? Enum.Parse(targetType, value, true) :
                Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        public static string GetString(Dictionary<string, object> row, string fieldName)
        {
            return row.TryGetValue(fieldName, out var value)
                ? Convert.ToString(value)?.Trim() ?? ""
                : "";
        }

        public static decimal GetDecimal(Dictionary<string, object> row, string fieldName)
        {
            if (!row.TryGetValue(fieldName, out var value)) return 0;
            if (value == null) return 0;

            decimal.TryParse(Convert.ToString(value), out var result);
            return result;
        }

        public static DataTable ConvertToDataTable(object? data, string? tableName = null)
        {
            var resolvedName = NormalizeNullableText(tableName) ?? "Data";
            if (data == null) return new DataTable(resolvedName);
            if (data is DataSet dataSet)
            {
                if (dataSet.Tables.Contains(resolvedName))
                {
                    var t = dataSet.Tables[resolvedName]!;
                    if (string.IsNullOrWhiteSpace(t.TableName)) t.TableName = resolvedName;
                    return t;
                }
                return dataSet.Tables.Count > 0 ? dataSet.Tables[0]! : new DataTable(resolvedName);
            }
            if (data is DataTable dt)
            {
                if (string.IsNullOrWhiteSpace(dt.TableName)) dt.TableName = resolvedName;
                return dt;
            }
            if (TryResolveMappedDataSource(data, resolvedName, out var mapped))
                return ConvertToDataTable(mapped, resolvedName);
            if (data is IEnumerable enumerable && data is not string)
                return ConvertEnumerableToDataTable(enumerable, resolvedName);
            return ConvertEnumerableToDataTable(new[] { data }, resolvedName);
        }

        private static bool TryResolveMappedDataSource(object data, string tableName, out object? mappedValue)
        {
            mappedValue = null;
            if (data is IDictionary<string, object?> d1)
            {
                if (d1.TryGetValue(tableName, out var v1)) { mappedValue = v1; return true; }
                mappedValue = d1.Values.FirstOrDefault(x => x != null);
                return mappedValue != null;
            }
            if (data is IDictionary<string, object> d2)
            {
                if (d2.TryGetValue(tableName, out var v2)) { mappedValue = v2; return true; }
                mappedValue = d2.Values.FirstOrDefault(x => x != null);
                return mappedValue != null;
            }
            var prop = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            if (prop == null) return false;
            mappedValue = prop.GetValue(data);
            return mappedValue != null;
        }

        private static DataTable ConvertEnumerableToDataTable(IEnumerable source, string tableName)
        {
            var table = new DataTable(tableName);
            var comparer = StringComparer.OrdinalIgnoreCase;
            foreach (var item in source)
            {
                if (item == null) continue;
                IReadOnlyDictionary<string, object?>? dict = null;
                if (item is IDictionary<string, object?> dn)
                    dict = new Dictionary<string, object?>(dn, comparer);
                else if (item is IDictionary<string, object> dp)
                    dict = dp.ToDictionary(x => x.Key, x => (object?)x.Value, comparer);
                else if (item is ExpandoObject expando)
                    dict = new Dictionary<string, object?>((IDictionary<string, object?>)expando, comparer);
                if (dict != null)
                {
                    foreach (var pair in dict)
                    {
                        var col = NormalizeNullableText(pair.Key);
                        if (col != null && !table.Columns.Contains(col))
                        {
                            var ct = pair.Value?.GetType() ?? typeof(string);
                            table.Columns.Add(col, ct == typeof(object) ? typeof(string) : ct);
                        }
                    }
                    var row = table.NewRow();
                    foreach (var pair in dict)
                    {
                        var col = NormalizeNullableText(pair.Key);
                        if (col != null) row[col] = pair.Value ?? DBNull.Value;
                    }
                    table.Rows.Add(row);
                    continue;
                }
                var itemType = item.GetType();
                if (IsScalarType(itemType))
                {
                    if (!table.Columns.Contains("VALUE"))
                        table.Columns.Add("VALUE", Nullable.GetUnderlyingType(itemType) ?? itemType);
                    var row = table.NewRow();
                    row["VALUE"] = item;
                    table.Rows.Add(row);
                    continue;
                }
                foreach (var p in itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (table.Columns.Contains(p.Name)) continue;
                    var ct = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    table.Columns.Add(p.Name, ct == typeof(object) ? typeof(string) : ct);
                }
                var objRow = table.NewRow();
                foreach (var p in itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    objRow[p.Name] = p.GetValue(item) ?? DBNull.Value;
                table.Rows.Add(objRow);
            }
            return table;
        }

        private static bool IsScalarType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive || underlying == typeof(string) ||
                   underlying == typeof(decimal) || underlying == typeof(DateTime) ||
                   underlying == typeof(Guid);
        }

        private static string FormatSqlWithParams(string sql, object? param, CommandType? commandType = null)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql ?? string.Empty;

            var dict = ToParamDictionary(param);

            if (commandType == CommandType.StoredProcedure)
            {
                if (dict.Count == 0)
                    return $"CALL {sql}();";

                var named = string.Join(", ", dict.Select(kv => $"{kv.Key}={FormatParamValue(kv.Value)}"));

                var positional = string.Join(", ", dict.Select(kv => FormatParamValue(kv.Value)));

                return $"-- {named}{Environment.NewLine}CALL {sql}({positional});";
            }

            foreach (var kv in dict.OrderByDescending(x => x.Key.Length))
            {
                var name = kv.Key.TrimStart('@');
                var value = FormatParamValue(kv.Value);

                sql = Regex.Replace(
                    sql,
                    $@"(?<!@)@{Regex.Escape(name)}\b",
                    value,
                    RegexOptions.IgnoreCase
                );
            }

            return sql;
        }

        private static Dictionary<string, object?> ToParamDictionary(object? param)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (param == null)
                return dict;

            if (param is DynamicParameters dp)
            {
                foreach (var name in dp.ParameterNames.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    object? value = null;
                    try
                    {
                        value = dp.Get<dynamic>(name);
                    }
                    catch
                    {
                        value = null;
                    }

                    dict[name.TrimStart('@')] = value;
                }

                return dict;
            }

            if (param is IDictionary<string, object> map1)
            {
                foreach (var kv in map1)
                    dict[kv.Key.TrimStart('@')] = kv.Value;

                return dict;
            }

            if (param is IDictionary<string, object?> map2)
            {
                foreach (var kv in map2)
                    dict[kv.Key.TrimStart('@')] = kv.Value;

                return dict;
            }

            if (param is IDictionary map)
            {
                foreach (DictionaryEntry item in map)
                {
                    var key = Convert.ToString(item.Key)?.TrimStart('@');
                    if (!string.IsNullOrWhiteSpace(key))
                        dict[key] = item.Value;
                }

                return dict;
            }

            foreach (var p in param.GetType()
                                   .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(p => p.CanRead))
            {
                dict[p.Name.TrimStart('@')] = p.GetValue(param);
            }

            return dict;
        }

        private static string FormatParamValue(object? value)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            if (value is string s)
                return $"'{s.Replace("'", "''")}'";

            if (value is char c)
                return $"'{c.ToString().Replace("'", "''")}'";

            if (value is DateTime dt)
                return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";

            if (value is DateTimeOffset dto)
                return $"'{dto:yyyy-MM-dd HH:mm:ss.fff zzz}'";

            if (value is bool b)
                return b ? "1" : "0";

            if (value is byte[] bytes)
                return "0x" + Convert.ToHexString(bytes);

            if (value is Enum)
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            if (value is IFormattable f)
                return f.ToString(null, CultureInfo.InvariantCulture);

            if (value is IEnumerable enumerable && value is not string && value is not byte[])
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                    items.Add(FormatParamValue(item));

                return $"({string.Join(", ", items)})";
            }

            return $"'{value.ToString()?.Replace("'", "''")}'";
        }

        public static void LogQuery(string sql, object? param = null, long? elapsedMs = null)
        {
            try
            {
                var formatted = FormatSqlWithParams(sql, param);

                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    formatted = Regex.Replace(formatted, @"\s+", " ").Trim();
                }

                AppendUserLogLine(formatted);
            }
            catch
            {
            }
        }

        public static void LogDebug(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                AppendUserLogLine(message.Trim());
            }
            catch
            {
            }
        }

        private static void AppendUserLogLine(string message)
        {
            // Never write under publish/SMB ContentRoot on macOS — that recycled the API mid-import.
            var logDir = AmnoteRuntimePaths.CombineAppData(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var context = http?.HttpContext;
            var companyCd = CompanyRouteContextMiddleware.TryGetCompanyCode(context, out var resolvedCompanyCd)
                ? resolvedCompanyCd
                : "SYSTEM";
            var username = User?.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(username))
            {
                username = context?.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) == true
                    ? "AUTH"
                    : "ANONYMOUS";
            }

            var logFile = Path.Combine(logDir, $"{NormalizeLogFilePart(companyCd)}_{NormalizeLogFilePart(username)}.log");
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            var line = $"{time} : {message}{Environment.NewLine}";
            File.AppendAllText(logFile, line);
        }

        private static string NormalizeLogFilePart(string value)
        {
            var normalized = Regex.Replace(value.Trim(), "[^A-Za-z0-9_-]", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "SYSTEM";
            }

            return normalized.Length > 64 ? normalized[..64] : normalized;
        }

        public static DataTable LoadCurrentResultSetToDataTable(IDataReader reader, string tableName)
        {
            var table = new DataTable(tableName);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    columnName = $"Column{i + 1}";
                }

                columnName = MakeUniqueColumnName(table, columnName);

                var fieldType = reader.GetFieldType(i);

                var column = new DataColumn(columnName, fieldType)
                {
                    AllowDBNull = true
                };

                table.Columns.Add(column);
            }

            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);

                for (var i = 0; i < values.Length; i++)
                {
                    if (values[i] == null)
                    {
                        values[i] = DBNull.Value;
                    }
                }

                table.Rows.Add(values);
            }

            return table;
        }

        private static string MakeUniqueColumnName(DataTable table, string columnName)
        {
            if (!table.Columns.Contains(columnName))
            {
                return columnName;
            }

            var index = 1;
            var newName = $"{columnName}_{index}";

            while (table.Columns.Contains(newName))
            {
                index++;
                newName = $"{columnName}_{index}";
            }

            return newName;
        }
    }

    public enum Net_DB
    {
        Net_DB_Company,
        Net_DB_Manager
    }
}
