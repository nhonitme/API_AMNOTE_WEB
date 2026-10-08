using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Repositories
{
    public class SysCodeSequenceRepository : ISysCodeSequenceRepository
    {
        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromSeconds(30);
        private static readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<SysCodeSequence>>>> SequenceLoads = new(StringComparer.OrdinalIgnoreCase);

        private readonly DapperExecutor _db;
        private readonly IMemoryCache _memoryCache;

        public SysCodeSequenceRepository(DapperExecutor db, IMemoryCache memoryCache)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public async Task<IEnumerable<SysCodeSequence>> GetSequencesAsync(string companyCd, string? objectType = null)
        {
            var sequences = await GetAllSequencesCachedAsync(companyCd);
            var normalizedObjectType = NormalizeNullableUpper(objectType);
            return normalizedObjectType == null
                ? sequences
                : sequences.Where(item => item.OBJECT_TYPE.Equals(normalizedObjectType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<SysCodeSequence?> GetSequenceAsync(string companyCd, long id)
        {
            var sequences = await GetAllSequencesCachedAsync(companyCd);
            return sequences.FirstOrDefault(item => item.ID == id);
        }

        public async Task<int> UpsertSequenceAsync(string companyCd, SysCodeSequenceRequest request)
        {
            var objectType = NormalizeRequiredUpper(request.OBJECT_TYPE, nameof(request.OBJECT_TYPE));
            var menuCode = NormalizeNullableUpper(request.MENU_CODE) ?? string.Empty;
            var codeField = NormalizeNullableUpper(request.CODE_FIELD) ?? string.Empty;
            var resetType = NormalizeResetType(request.RESET_TYPE);
            var isUse = NormalizeIsUse(request.IS_USE);
            var numberLength = request.NUMBER_LENGTH.GetValueOrDefault(6);
            if (numberLength <= 0)
            {
                throw new ArgumentException("NUMBER_LENGTH must be greater than 0");
            }

            const string query = @"CALL setSysCodeSequence(
                @p_ID,
                @p_COMPANY_CD,
                @p_OBJECT_TYPE,
                @p_MENU_CODE,
                @p_CODE_FIELD,
                @p_PREFIX,
                @p_SUFFIX,
                @p_CODE_PATTERN,
                @p_CURRENT_NO,
                @p_NUMBER_LENGTH,
                @p_RESET_TYPE,
                @p_RESET_KEY,
                @p_IS_USE)";

            var result = await _db.ExecuteAsync(Net_DB.Net_DB_Company, query, new
            {
                p_ID = request.ID.GetValueOrDefault(),
                p_COMPANY_CD = companyCd,
                p_OBJECT_TYPE = objectType,
                p_MENU_CODE = menuCode,
                p_CODE_FIELD = codeField,
                p_PREFIX = Common.NormalizeNullableText(request.PREFIX) ?? string.Empty,
                p_SUFFIX = Common.NormalizeNullableText(request.SUFFIX) ?? string.Empty,
                p_CODE_PATTERN = NormalizeCodePattern(request.CODE_PATTERN),
                p_CURRENT_NO = Math.Max(0, request.CURRENT_NO.GetValueOrDefault()),
                p_NUMBER_LENGTH = numberLength,
                p_RESET_TYPE = resetType,
                p_RESET_KEY = Common.NormalizeNullableText(request.RESET_KEY) ?? string.Empty,
                p_IS_USE = isUse
            });

            ClearCompanyCache(companyCd);
            return result;
        }

        public async Task<int> DeleteSequenceAsync(string companyCd, long id)
        {
            const string query = "CALL delSysCodeSequence(@p_COMPANY_CD, @p_ID)";
            var result = await _db.ExecuteAsync(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_ID = id
            });

            ClearCompanyCache(companyCd);
            return result;
        }

        public async Task<SysCodeSequencePreview?> PreviewAsync(string companyCd, string objectType, DateTime? baseDate = null)
        {
            const string query = "CALL getSysCodeSequencePreview(@p_COMPANY_CD, @p_OBJECT_TYPE, @p_BASE_DATE)";

            return (await _db.QueryAsync<SysCodeSequencePreview>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = NormalizeRequiredUpper(companyCd, nameof(companyCd)),
                p_OBJECT_TYPE = NormalizeRequiredUpper(objectType, nameof(objectType)),
                p_BASE_DATE = Common.NormalizeSequenceBaseDate(baseDate)
            })).FirstOrDefault();
        }

        public async Task<SysCodeSequencePreview?> PreviewByContextAsync(string companyCd, string menuCode, string codeField, DateTime? baseDate = null)
        {
            var sequence = await GetSequenceByContextCachedAsync(companyCd, menuCode, codeField);
            if (sequence == null)
            {
                return null;
            }

            var preview = await PreviewAsync(companyCd, sequence.OBJECT_TYPE, baseDate);
            if (preview != null)
            {
                preview.OBJECT_TYPE = sequence.OBJECT_TYPE;
                preview.MENU_CODE = sequence.MENU_CODE;
                preview.CODE_FIELD = sequence.CODE_FIELD;
            }

            return preview;
        }

        public async Task<IReadOnlyList<SysCodeSequencePreview>> PreviewManyAsync(string companyCd, IEnumerable<string> objectTypes, DateTime? baseDate = null)
        {
            var normalizedObjectTypes = (objectTypes ?? Enumerable.Empty<string>())
                .Select(NormalizeNullableUpper)
                .Where(item => item != null)
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedObjectTypes.Count == 0)
            {
                return Array.Empty<SysCodeSequencePreview>();
            }

            var previews = new List<SysCodeSequencePreview>(normalizedObjectTypes.Count);
            foreach (var objectType in normalizedObjectTypes)
            {
                var preview = await PreviewAsync(companyCd, objectType, baseDate);
                if (preview != null)
                {
                    previews.Add(preview);
                }
            }

            return previews;
        }

        public async Task<string?> ResolveCodeAsync(DapperSession session, string companyCd, string objectType, string? requestedCode, DateTime? baseDate = null)
        {
            var normalizedCompanyCd = NormalizeRequiredUpper(companyCd, nameof(companyCd));
            var normalizedObjectType = NormalizeRequiredUpper(objectType, nameof(objectType));
            var normalizedRequested = Common.NormalizeNullableText(requestedCode);
            var resolvedBaseDate = Common.NormalizeSequenceBaseDate(baseDate);

            var result = await QueryResolveAsync(
                session,
                normalizedCompanyCd,
                normalizedObjectType,
                normalizedRequested,
                resolvedBaseDate);

            if (!string.IsNullOrWhiteSpace(normalizedRequested))
            {
                if (ShouldIncrementSequenceCounter(result))
                {
                    await QueryResolveAsync(
                        session,
                        normalizedCompanyCd,
                        normalizedObjectType,
                        null,
                        resolvedBaseDate);
                }

                ClearCompanyCache(companyCd);
                return normalizedRequested;
            }

            if (ShouldIncrementSequenceCounter(result))
            {
                result = await QueryResolveAsync(
                    session,
                    normalizedCompanyCd,
                    normalizedObjectType,
                    null,
                    resolvedBaseDate);
            }

            ClearCompanyCache(companyCd);
            return Common.NormalizeNullableText(result?.GENERATED_CD);
        }

        public async Task<string?> ResolveCodeByContextAsync(DapperSession session, string companyCd, string menuCode, string codeField, string? requestedCode, DateTime? baseDate = null)
        {
            var normalizedCompanyCd = NormalizeRequiredUpper(companyCd, nameof(companyCd));
            var sequence = await QuerySequenceByContextAsync(session, normalizedCompanyCd, menuCode, codeField);
            if (sequence == null)
            {
                return Common.NormalizeNullableText(requestedCode);
            }

            return await ResolveCodeAsync(
                session,
                normalizedCompanyCd,
                sequence.OBJECT_TYPE,
                requestedCode,
                baseDate);
        }

        private static async Task<SysCodeSequenceResolveResult?> QueryResolveAsync(
            DapperSession session,
            string companyCd,
            string objectType,
            string? requestedCode,
            DateTime baseDate)
        {
            const string query = "CALL getSysCodeSequenceNext(@p_COMPANY_CD, @p_OBJECT_TYPE, @p_REQUESTED_CD, @p_BASE_DATE)";

            return (await session.QueryAsync<SysCodeSequenceResolveResult>(query, new
            {
                p_COMPANY_CD = companyCd,
                p_OBJECT_TYPE = objectType,
                p_REQUESTED_CD = requestedCode,
                p_BASE_DATE = baseDate
            }, commandTimeout: 60)).FirstOrDefault();
        }

        private static bool ShouldIncrementSequenceCounter(SysCodeSequenceResolveResult? result)
        {
            if (result == null)
            {
                return false;
            }

            return string.Equals(result.WAS_SEQUENCE_USED, "1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(result.WAS_CURRENT_NO_UPDATED, "1", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<IReadOnlyList<SysCodeSequence>> GetAllSequencesCachedAsync(string companyCd)
        {
            var normalizedCompanyCd = NormalizeRequiredUpper(companyCd, nameof(companyCd));
            var cacheKey = BuildCompanyCacheKey(normalizedCompanyCd);

            if (_memoryCache.TryGetValue<IReadOnlyList<SysCodeSequence>>(cacheKey, out var cachedSequences))
            {
                return cachedSequences ?? Array.Empty<SysCodeSequence>();
            }

            var lazyLoad = SequenceLoads.GetOrAdd(cacheKey, _ => new Lazy<Task<IReadOnlyList<SysCodeSequence>>>(
                () => LoadAllSequencesAsync(normalizedCompanyCd),
                LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                var sequences = await lazyLoad.Value;
                _memoryCache.Set(cacheKey, sequences, BuildCacheOptions());
                return sequences;
            }
            finally
            {
                SequenceLoads.TryRemove(cacheKey, out _);
            }
        }

        private async Task<IReadOnlyList<SysCodeSequence>> LoadAllSequencesAsync(string companyCd)
        {
            const string query = "CALL getSysCodeSequences(@p_COMPANY_CD, @p_OBJECT_TYPE)";
            return (await _db.QueryAsync<SysCodeSequence>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_OBJECT_TYPE = (string?)null
            })).ToList();
        }

        private async Task<SysCodeSequence?> GetSequenceByContextCachedAsync(string companyCd, string menuCode, string codeField)
        {
            var normalizedCompanyCd = NormalizeRequiredUpper(companyCd, nameof(companyCd));
            var normalizedMenuCode = NormalizeRequiredUpper(menuCode, nameof(menuCode));
            var normalizedCodeField = NormalizeRequiredUpper(codeField, nameof(codeField));
            var sequences = await GetAllSequencesCachedAsync(normalizedCompanyCd);

            return sequences
                .Where(item => IsActive(item) && MatchesContext(item, normalizedMenuCode, normalizedCodeField))
                .OrderByDescending(item => item.ID)
                .FirstOrDefault();
        }

        private static async Task<SysCodeSequence?> QuerySequenceByContextAsync(
            DapperSession session,
            string companyCd,
            string menuCode,
            string codeField)
        {
            const string query = @"
                SELECT
                    ID,
                    COMPANY_CD,
                    OBJECT_TYPE,
                    IFNULL(MENU_CODE, '') AS MENU_CODE,
                    IFNULL(CODE_FIELD, '') AS CODE_FIELD,
                    PREFIX,
                    SUFFIX,
                    IFNULL(CODE_PATTERN, '{PREFIX}{NO}{SUFFIX}') AS CODE_PATTERN,
                    CURRENT_NO,
                    NUMBER_LENGTH,
                    RESET_TYPE,
                    RESET_KEY,
                    IS_USE,
                    CREATE_AT,
                    UPDATE_AT
                FROM sys_code_sequence
                WHERE COMPANY_CD = @p_COMPANY_CD
                  AND MENU_CODE = @p_MENU_CODE
                  AND CODE_FIELD = @p_CODE_FIELD
                  AND IS_USE = '1'
                ORDER BY ID DESC
                LIMIT 1";

            return (await session.QueryAsync<SysCodeSequence>(query, new
            {
                p_COMPANY_CD = companyCd,
                p_MENU_CODE = NormalizeRequiredUpper(menuCode, nameof(menuCode)),
                p_CODE_FIELD = NormalizeRequiredUpper(codeField, nameof(codeField))
            })).FirstOrDefault();
        }

        private static bool MatchesContext(SysCodeSequence sequence, string menuCode, string codeField)
            => string.Equals(sequence.MENU_CODE, menuCode, StringComparison.OrdinalIgnoreCase)
               && string.Equals(sequence.CODE_FIELD, codeField, StringComparison.OrdinalIgnoreCase);

        private static bool IsActive(SysCodeSequence sequence)
            => string.Equals(sequence.IS_USE, "1", StringComparison.OrdinalIgnoreCase);

        private void ClearCompanyCache(string companyCd)
        {
            var normalizedCompanyCd = NormalizeNullableUpper(companyCd);
            if (normalizedCompanyCd == null)
            {
                return;
            }

            var cacheKey = BuildCompanyCacheKey(normalizedCompanyCd);
            _memoryCache.Remove(cacheKey);
            SequenceLoads.TryRemove(cacheKey, out _);
        }

        public int ClearAllCache()
        {
            var removed = 0;

            foreach (var cacheKey in SequenceLoads.Keys.ToArray())
            {
                _memoryCache.Remove(cacheKey);
                if (SequenceLoads.TryRemove(cacheKey, out _))
                {
                    removed++;
                }
            }

            return removed;
        }

        private static MemoryCacheEntryOptions BuildCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheAbsoluteExpiration,
                SlidingExpiration = CacheSlidingExpiration
            };
        }

        private static string BuildCompanyCacheKey(string companyCd)
            => $"sys-code-sequence:{companyCd}";

        private static string NormalizeRequiredUpper(string? value, string parameterName)
        {
            var normalized = NormalizeNullableUpper(value);
            if (normalized == null)
            {
                throw new ArgumentException($"{parameterName} is required");
            }

            return normalized;
        }

        private static string? NormalizeNullableUpper(string? value)
            => Common.NormalizeNullableText(value)?.ToUpperInvariant();

        private static string NormalizeResetType(string? value)
        {
            return NormalizeNullableUpper(value) switch
            {
                "YEAR" => "YEAR",
                "MONTH" => "MONTH",
                _ => "NONE"
            };
        }

        private static string NormalizeIsUse(string? value)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized == null)
            {
                return "1";
            }

            return normalized == "1" ? "1" : "0";
        }

        private static string NormalizeCodePattern(string? value)
        {
            var pattern = Common.NormalizeNullableText(value) ?? "{PREFIX}{NO}{SUFFIX}";
            if (pattern.Length > 100)
            {
                throw new ArgumentException("CODE_PATTERN length must be less than or equal to 100");
            }

            if (!pattern.Contains("{NO}", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("CODE_PATTERN must include {NO}");
            }

            if (Regex.IsMatch(pattern, @"\{(?!PREFIX\}|YYYY\}|YY\}|MM\}|DD\}|NO\}|SUFFIX\})[^}]*\}", RegexOptions.IgnoreCase))
            {
                throw new ArgumentException("CODE_PATTERN contains unsupported token");
            }

            return pattern;
        }
    }
}
