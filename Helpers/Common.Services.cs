using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Helpers
{
    public static partial class Common
    {
        public static IServiceProvider? ServiceProvider { get; set; }

        private static IHttpContextAccessor? http => ServiceProvider?.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
        private static ClaimsPrincipal? User => http?.HttpContext?.User;

        private static IServiceScopeFactory ResolveScopeFactory()
        {
            if (ServiceProvider == null)
                throw new InvalidOperationException("Common.ServiceProvider is not initialized. Set it in Program.cs after building the app: Common.ServiceProvider = app.Services;");

            return ServiceProvider.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory
                ?? throw new InvalidOperationException("IServiceScopeFactory not available in DI container");
        }

        private static async Task<string> GetMessageAsync(string key, string lang = "VIET", string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback ?? string.Empty;

            var scopeFactory = ResolveScopeFactory();
            using var scope = scopeFactory.CreateScope();
            var languageRepository = scope.ServiceProvider.GetService<ILanguageRepository>()
                ?? throw new InvalidOperationException("ILanguageRepository not registered in DI container");

            var trimmedKey = key.Trim();
            var msgs = await languageRepository.getLanguageInfos(new[] { trimmedKey });
            var msg = msgs?.FirstOrDefault(m => string.Equals(m.KEY?.Trim(), trimmedKey, StringComparison.OrdinalIgnoreCase));
            if (msg == null)
                return fallback ?? key;

            var val = GetMessageValue(msg, lang);
            return string.IsNullOrWhiteSpace(val) ? (fallback ?? key) : val;
        }

        public static async Task<Dictionary<string, string>> GetMessagesAsync(IEnumerable<string> keys, string lang = "VIET")
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (keys == null) return result;

            var keyList = keys.Where(k => !string.IsNullOrWhiteSpace(k))
                              .Select(k => k.Trim())
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            if (!keyList.Any()) return result;

            var scopeFactory = ResolveScopeFactory();
            using var scope = scopeFactory.CreateScope();
            var languageRepository = scope.ServiceProvider.GetService<ILanguageRepository>()
                ?? throw new InvalidOperationException("ILanguageRepository not registered in DI container");

            var msgs = await languageRepository.getLanguageInfos(keyList);

            foreach (var k in keyList)
            {
                string label = k;
                var msg = msgs?.FirstOrDefault(m => string.Equals(m.KEY?.Trim(), k, StringComparison.OrdinalIgnoreCase));
                if (msg != null)
                {
                    var val = GetMessageValue(msg, lang);
                    label = string.IsNullOrWhiteSpace(val) ? k : val;
                }

                result[k] = label ?? k;
            }

            return result;
        }

        public static string getLanguageV2(string key, string lang = "VIET")
        {
            if (key != "")
            {
                var scopeFactory = ResolveScopeFactory();
                using var scope = scopeFactory.CreateScope();
                var languageRepository = scope.ServiceProvider.GetService<ILanguageRepository>()
                    ?? throw new InvalidOperationException("ILanguageRepository not registered in DI container");

                var trimmedKey = key.Trim();
                var msgs = languageRepository.getLanguageInfos(new[] { trimmedKey });

                var list = msgs.GetAwaiter().GetResult();
                if (list == null || !list.Any()) return "";

                var msg = list.FirstOrDefault(m => string.Equals(m.KEY?.Trim(), trimmedKey, StringComparison.OrdinalIgnoreCase));
                return msg == null ? string.Empty : GetMessageValue(msg, lang);
            }
            return key;
        }

        public static Task<string> getLanguage(string key, string lang = "VIET")
            => GetMessageAsync(key, lang, key);

        public static Task<Dictionary<string, string>> getLanguage(IEnumerable<string> keys, string lang = "VIET")
            => GetMessagesAsync(keys, lang);

        public const string RequiredMessageKey = "REQUIRED";

        public static async Task<string> BuildCombinedMessageAsync(string fieldKey, string messageKey, string? lang = null)
        {
            var resolvedLang = NormalizeLanguageCode(lang ?? GetCurrentLanguage());
            return (await getLanguage(fieldKey, resolvedLang)) + " " + (await getLanguage(messageKey, resolvedLang));
        }

        public static Task<string> GetFieldRequiredMessageAsync(string fieldKey, string? lang = null, string? fieldFallback = null)
        {
            var resolvedLang = NormalizeLanguageCode(lang ?? GetCurrentLanguage());
            return BuildFieldRequiredMessageAsync(fieldKey, resolvedLang, fieldFallback);
        }

        public static async Task<string> BuildFieldRequiredMessageAsync(string fieldKey, string lang, string? fieldFallback = null)
        {
            var fieldLabel = await getLanguage(fieldKey, lang);
            if (string.IsNullOrWhiteSpace(fieldLabel) || string.Equals(fieldLabel, fieldKey, StringComparison.OrdinalIgnoreCase))
            {
                fieldLabel = fieldFallback ?? fieldKey;
            }

            var requiredLabel = await getLanguage(RequiredMessageKey, lang);
            if (string.IsNullOrWhiteSpace(requiredLabel)
                || string.Equals(requiredLabel, RequiredMessageKey, StringComparison.OrdinalIgnoreCase))
            {
                requiredLabel = "is required";
            }

            return $"{fieldLabel} {requiredLabel}";
        }

        public static string GetFieldRequiredMessage(string fieldKey, string? lang = null, string? fieldFallback = null)
        {
            var resolvedLang = NormalizeLanguageCode(lang ?? GetCurrentLanguage());
            var fieldLabel = getLanguageV2(fieldKey, resolvedLang);
            if (string.IsNullOrWhiteSpace(fieldLabel))
            {
                fieldLabel = fieldFallback ?? fieldKey;
            }

            var requiredLabel = getLanguageV2(RequiredMessageKey, resolvedLang);
            if (string.IsNullOrWhiteSpace(requiredLabel))
            {
                requiredLabel = "is required";
            }

            return $"{fieldLabel} {requiredLabel}";
        }

        public static ArgumentException FieldRequiredArgument(string fieldKey, string? lang = null, string? fieldFallback = null)
            => new ArgumentException(GetFieldRequiredMessage(fieldKey, lang, fieldFallback));

        public static string GetFieldRequiredAtLineMessage(string fieldKey, int lineNo, string? lang = null, string? fieldFallback = null)
            => $"{GetFieldRequiredMessage(fieldKey, lang, fieldFallback)} ({lineNo})";

        public static ArgumentException FieldRequiredAtLineArgument(string fieldKey, int lineNo, string? lang = null, string? fieldFallback = null)
            => new ArgumentException(GetFieldRequiredAtLineMessage(fieldKey, lineNo, lang, fieldFallback));

        public static string GetMessageValue(t_message_info msg, string? lang)
        {
            var propName = NormalizeLanguageCode(lang);
            var value = msg.GetType().GetProperty(propName)?.GetValue(msg) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            foreach (var fallbackColumn in MessageFallbackColumns)
            {
                value = msg.GetType().GetProperty(fallbackColumn)?.GetValue(msg) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Excel template columns: FIELD_NAME → CAPTION.
        /// Use Keys for cell/property lookup; use Values (CAPTION) with ResolveDisplayLabel for headers.
        /// </summary>
        public static async Task<Dictionary<string, string>> GetExcelTemplateKeysAsync(string moduleCd, string? companyCd = null)
        {
            if (string.IsNullOrWhiteSpace(moduleCd))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var columns = await GetExcelTemplateColumnInfosAsync(moduleCd, companyCd);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                var fieldName = NormalizeNullableText(column.FIELD_NAME);
                if (fieldName == null || result.ContainsKey(fieldName))
                    continue;

                var caption = NormalizeNullableText(column.CAPTION) ?? fieldName;
                result[fieldName] = caption;
            }

            return result;
        }

        public static async Task<List<ExcelTemplateColumnInfo>> GetExcelTemplateColumnInfosAsync(string moduleCd, string _companycd = null)
        {
            if (string.IsNullOrWhiteSpace(moduleCd))
                return new List<ExcelTemplateColumnInfo>();

            var companyCd = _companycd + "";
            if (companyCd + "" == "")
                companyCd = GetCompanyCode();

            var scopeFactory = ResolveScopeFactory();
            using var scope = scopeFactory.CreateScope();
            var systemService = scope.ServiceProvider.GetService<ISystemService>()
                ?? throw new InvalidOperationException("ISystemService not registered in DI container");

            var columns = await systemService.GetExcelTemplateColumnInfosAsync(companyCd, moduleCd) ?? new List<ExcelTemplateColumnInfo>();
            return NormalizeTemplateColumnInfos(columns);
        }

        public static string GetCompanyCode()
        {
            var context = http?.HttpContext;
            if (CompanyRouteContextMiddleware.TryGetCompanyCode(context, out var companyCd))
            {
                return companyCd;
            }

            LogMissingCompanyContext(context);
            throw new UnauthorizedAccessException("Company code not found in request context");
        }

        private static void LogMissingCompanyContext(HttpContext? context)
        {
            try
            {
                var loggerFactory = ServiceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                var logger = loggerFactory?.CreateLogger("CompanyContext");
                if (logger == null)
                {
                    return;
                }

                var itemKeys = context == null
                    ? string.Empty
                    : string.Join(",", context.Items.Keys.Select(key => key?.ToString()).Where(key => !string.IsNullOrWhiteSpace(key)));

                logger.LogWarning(
                    "Common.GetCompanyCode failed. Method: {Method}, Path: {Path}, Query: {Query}, HeaderCompanyCd: {HeaderCompanyCd}, ItemKeys: {ItemKeys}, UserAuthenticated: {UserAuthenticated}",
                    context?.Request.Method,
                    context?.Request.Path.Value,
                    context?.Request.QueryString.Value,
                    context?.Request.Headers[CompanyRouteContextMiddleware.CompanyCdHeaderName].ToString(),
                    itemKeys,
                    context?.User?.Identity?.IsAuthenticated);
            }
            catch
            {
            }
        }

        public static string GetDatabaseName()
        {
            var companyCd = GetCompanyCode();
            var scopeFactory = ResolveScopeFactory();
            using var scope = scopeFactory.CreateScope();
            var resolver = scope.ServiceProvider.GetService<ICompanyDatabaseResolver>()
                ?? throw new InvalidOperationException("ICompanyDatabaseResolver not registered in DI container");

            return resolver.ResolveDatabaseName(companyCd);
        }

        public static string GetUserId()
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User ID not found in token");
            return userId;
        }

        public static long GetUserPkId()
        {
            var raw = User?.FindFirst(AuthClaimTypes.UserPkId)?.Value;
            if (long.TryParse(raw, out var userPkId) && userPkId > 0)
            {
                return userPkId;
            }

            return 0;
        }

        public static string GetUserName()
        {
            var username = User?.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrWhiteSpace(username))
            {
                return username.Trim();
            }

            return GetUserId();
        }

        public static async Task<bool> HasPermissionAsync(string menuCode, string permissionKey)
        {
            var normalizedMenuCode = string.IsNullOrWhiteSpace(menuCode) ? string.Empty : menuCode.Trim();
            var normalizedPermissionKey = string.IsNullOrWhiteSpace(permissionKey) ? string.Empty : permissionKey.Trim();
            if (normalizedMenuCode.Length == 0 || normalizedPermissionKey.Length == 0)
            {
                return false;
            }

            var companyCd = GetCompanyCode();
            var userId = GetUserId();
            var requestCacheKey = $"system-user-permissions:{companyCd.Trim().ToUpperInvariant()}:{userId.Trim().ToUpperInvariant()}";
            var context = http?.HttpContext;
            List<SysUserPermission>? records = null;

            if (context?.Items.TryGetValue(requestCacheKey, out var cachedRecords) == true)
            {
                records = cachedRecords as List<SysUserPermission>;
            }

            if (records == null)
            {
                var scopeFactory = ResolveScopeFactory();
                using var scope = scopeFactory.CreateScope();
                var systemService = scope.ServiceProvider.GetService<ISystemService>()
                    ?? throw new InvalidOperationException("ISystemService not registered in DI container");

                records = (await systemService.GetUserPermissionsAsync(companyCd, userId)).ToList();
                if (context != null)
                {
                    context.Items[requestCacheKey] = records;
                }
            }

            return records.Any(record =>
                (record.ISDEL ?? "0") == "0"
                && string.Equals(record.MENU_CODE?.Trim(), normalizedMenuCode, StringComparison.OrdinalIgnoreCase)
                && record.HasPermission(normalizedPermissionKey));
        }

        public static string GetCurrentLanguage()
        {
            var lang = User?.FindFirst(AuthClaimTypes.Language)?.Value
                ?? User?.FindFirst("Lang")?.Value;

            if (!string.IsNullOrWhiteSpace(lang))
                return NormalizeLanguageCode(lang);

            return "VIET";
        }
    }
}
