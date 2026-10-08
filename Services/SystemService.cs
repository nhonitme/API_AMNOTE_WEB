using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace API_AMNOTE_WEB.Services
{
    public class SystemService : ISystemService
    {
        private const string SysCodeCacheScope = "system-sys-codes";
        private const string SysCodeAllCacheKey = "all";
        private const string ExcelTemplateColumnCacheScope = "system-excel-template-column-infos";
        private const string UserPermissionCacheScope = "system-user-permissions";
        private const string EtcDataCacheScope = "system-etc-data";
        private static readonly ConcurrentDictionary<string, Lazy<Task<List<SysCodeInfo>>>> SysCodeLoads = new(StringComparer.OrdinalIgnoreCase);

        private readonly ISystemRepository _repository;
        private readonly IMasterDataCacheService _cache;
        private readonly ISysConfigService _sysConfigService;
        private readonly IUserSettingService _userSettingService;
        private readonly ISysCodeSequenceRepository _sysCodeSequenceRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly IActivityLogService _activityLogService;
        private readonly DapperExecutor _db;
        private readonly ILogger<SystemService> _logger;

        public SystemService(
            ISystemRepository repository,
            IMasterDataCacheService cache,
            ISysConfigService sysConfigService,
            IUserSettingService userSettingService,
            ISysCodeSequenceRepository sysCodeSequenceRepository,
            IMemoryCache memoryCache,
            IActivityLogService activityLogService,
            DapperExecutor db,
            ILogger<SystemService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _sysConfigService = sysConfigService ?? throw new ArgumentNullException(nameof(sysConfigService));
            _userSettingService = userSettingService ?? throw new ArgumentNullException(nameof(userSettingService));
            _sysCodeSequenceRepository = sysCodeSequenceRepository ?? throw new ArgumentNullException(nameof(sysCodeSequenceRepository));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<SysCodeInfo>> GetSysCodesAsync(string companyCd, string? codeType = null, bool refresh = false)
        {
            var normalizedCodeType = NormalizeSysCodeType(codeType);
            var allCodes = refresh
                ? await RefreshAllSysCodesAsync()
                : await GetAllSysCodesCachedAsync();
            if (normalizedCodeType == null)
            {
                return allCodes.ToList();
            }

            return allCodes
                .Where(item => string.Equals(NormalizeSysCodeType(item.CODE_TYPE), normalizedCodeType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<List<string>> GetExcelTemplateKeysAsync(string companyCd, string moduleCd)
        {
            var columns = await GetExcelTemplateColumnInfosAsync(companyCd, moduleCd);
            return columns
                .Select(item => Common.NormalizeNullableText(item.FIELD_NAME))
                .Where(item => item != null)
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<ExcelTemplateColumnInfo>> GetExcelTemplateColumnInfosAsync(string companyCd, string moduleCd)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedModuleCd = Common.NormalizeRequiredText(moduleCd);
            if (normalizedModuleCd.Length == 0)
            {
                return new List<ExcelTemplateColumnInfo>();
            }

            return await _cache.GetOrCreateAsync(
                ExcelTemplateColumnCacheScope,
                normalizedCompanyCd,
                $"moduleCd={normalizedModuleCd}",
                async () => await _repository.GetExcelTemplateColumnInfosAsync(normalizedCompanyCd, normalizedModuleCd));
        }

        public async Task<IEnumerable<EtcInfo>> GetEtcInfoAsync(string companyCd, string etcType, string lang, string param1, string param2)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedEtcType = Common.NormalizeRequiredText(etcType);
            var normalizedLang = Common.NormalizeRequiredText(lang);
            var normalizedParam1 = Common.NormalizeRequiredText(param1);
            var normalizedParam2 = Common.NormalizeRequiredText(param2);

            return await _cache.GetOrCreateAsync(
                EtcDataCacheScope,
                normalizedCompanyCd,
                $"etcType={normalizedEtcType}|lang={normalizedLang}|param1={normalizedParam1}|param2={normalizedParam2}",
                async () => (await _repository.GetEtcInfoAsync(
                    normalizedCompanyCd,
                    normalizedEtcType,
                    normalizedLang,
                    normalizedParam1,
                    normalizedParam2)).ToList());
        }

        public async Task<IEnumerable<SysUserPermission>> GetUserPermissionsAsync(string companyCd, string userId, string? menuCode = null)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId);
            var normalizedMenuCode = Common.NormalizeNullableText(menuCode);

            if (normalizedCompanyCd.Length == 0 || normalizedUserId.Length == 0)
            {
                return new List<SysUserPermission>();
            }

            var permissions = await _cache.GetOrCreateAsync(
                UserPermissionCacheScope,
                normalizedCompanyCd,
                BuildUserPermissionCacheKey(normalizedUserId),
                () => LoadUserPermissionsAsync(normalizedCompanyCd, normalizedUserId));

            if (normalizedMenuCode == null)
            {
                return permissions.ToList();
            }

            return permissions
                .Where(item => string.Equals(Common.NormalizeNullableText(item.MENU_CODE), normalizedMenuCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<List<SysUserPermission>> RefreshUserPermissionsAsync(string companyCd, string userId)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId);

            if (normalizedCompanyCd.Length == 0 || normalizedUserId.Length == 0)
            {
                return new List<SysUserPermission>();
            }

            return await _cache.RefreshAsync(
                UserPermissionCacheScope,
                normalizedCompanyCd,
                BuildUserPermissionCacheKey(normalizedUserId),
                () => LoadUserPermissionsAsync(normalizedCompanyCd, normalizedUserId));
        }

        public async Task<int> SetUserPermissionAsync(string companyCd, SysUserPermissionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedRequest = BuildPermissionRequest(normalizedCompanyCd, request);
            var oldRecord = (await LoadUserPermissionsAsync(normalizedCompanyCd, normalizedRequest.USERID ?? string.Empty))
                .FirstOrDefault(item => string.Equals(Common.NormalizeNullableText(item.MENU_CODE), normalizedRequest.MENU_CODE, StringComparison.OrdinalIgnoreCase));
            var oldData = oldRecord == null ? string.Empty : JsonSerializer.Serialize(oldRecord);
            var newData = JsonSerializer.Serialize(normalizedRequest);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                var result = await _repository.SetUserPermissionAsync(session, normalizedCompanyCd, normalizedRequest);

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    "UPDATE",
                    "System",
                    "sys_user_permission",
                    normalizedRequest.MENU_CODE ?? string.Empty,
                    oldData,
                    newData,
                    $"Update permissions for {normalizedRequest.USERID}:{normalizedRequest.MENU_CODE}");

                session.Commit();
                await RefreshUserPermissionsAsync(normalizedCompanyCd, normalizedRequest.USERID ?? string.Empty);
                _logger.LogInformation("System user permissions updated. Company: {CompanyCd}, UserId: {UserId}, MenuCode: {MenuCode}", normalizedCompanyCd, normalizedRequest.USERID, normalizedRequest.MENU_CODE);
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public Task<int> ClearMasterDataCacheAsync(string scope, string? companyCd = null, bool global = false)
        {
            var normalizedScope = Common.NormalizeRequiredText(scope);
            if (normalizedScope.Length == 0)
            {
                throw new ArgumentException("scope is required", nameof(scope));
            }

            if (global)
            {
                return _cache.ClearGlobalAsync(normalizedScope);
            }

            return _cache.ClearAsync(normalizedScope, Common.NormalizeNullableText(companyCd));
        }

        public async Task<CacheClearSummary> ClearAllCacheAsync()
        {
            var masterDataRemoved = await _cache.ClearAllAsync();
            var sysConfigRemoved = await _sysConfigService.ClearCacheAsync();
            var userSettingRemoved = await _userSettingService.ClearCacheAsync();
            var sysCodeSequenceRemoved = _sysCodeSequenceRepository.ClearAllCache();
            var remainingMemoryCacheRemoved = CompactRemainingMemoryCache();

            SysCodeLoads.Clear();

            var summary = new CacheClearSummary
            {
                MasterData = masterDataRemoved,
                SysConfig = sysConfigRemoved,
                UserSetting = userSettingRemoved,
                SysCodeSequence = sysCodeSequenceRemoved,
                RemainingMemoryCache = remainingMemoryCacheRemoved
            };

            _logger.LogInformation(
                "Cleared all application caches. MasterData: {MasterData}, SysConfig: {SysConfig}, UserSetting: {UserSetting}, SysCodeSequence: {SysCodeSequence}, RemainingMemoryCache: {RemainingMemoryCache}, Total: {Total}",
                summary.MasterData,
                summary.SysConfig,
                summary.UserSetting,
                summary.SysCodeSequence,
                summary.RemainingMemoryCache,
                summary.Total);

            return summary;
        }

        private int CompactRemainingMemoryCache()
        {
            if (_memoryCache is not MemoryCache memoryCache)
            {
                return 0;
            }

            var remaining = memoryCache.Count;
            memoryCache.Compact(1.0);
            return remaining;
        }

        private async Task<List<SysUserPermission>> LoadUserPermissionsAsync(string companyCd, string userId)
        {
            var records = await _repository.GetUserPermissionsAsync(companyCd, userId);
            return (records ?? Enumerable.Empty<SysUserPermission>())
                .Where(item => !string.IsNullOrWhiteSpace(item.MENU_CODE))
                .GroupBy(item => item.MENU_CODE.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private async Task<List<SysCodeInfo>> GetAllSysCodesCachedAsync()
        {
            var lazyLoad = SysCodeLoads.GetOrAdd(
                SysCodeAllCacheKey,
                _ => new Lazy<Task<List<SysCodeInfo>>>(() =>
                    _cache.GetOrCreateGlobalAsync(SysCodeCacheScope, SysCodeAllCacheKey, LoadAllSysCodesAsync)));

            try
            {
                return await lazyLoad.Value;
            }
            finally
            {
                SysCodeLoads.TryRemove(SysCodeAllCacheKey, out _);
            }
        }

        private Task<List<SysCodeInfo>> RefreshAllSysCodesAsync()
        {
            return _cache.RefreshGlobalAsync(SysCodeCacheScope, SysCodeAllCacheKey, LoadAllSysCodesAsync);
        }

        private async Task<List<SysCodeInfo>> LoadAllSysCodesAsync()
        {
            var records = await _repository.GetSysCodeAsync(null);
            return (records ?? Enumerable.Empty<SysCodeInfo>())
                .Where(item => NormalizeSysCodeType(item.CODE_TYPE) != null && Common.NormalizeNullableText(item.CODE_CD) != null)
                .ToList();
        }

        private static SysUserPermissionRequest BuildPermissionRequest(string companyCd, SysUserPermissionRequest request)
        {
            var normalizedUserId = Common.NormalizeRequiredText(request.USERID);
            var normalizedMenuCode = Common.NormalizeRequiredText(request.MENU_CODE);

            if (companyCd.Length == 0)
            {
                throw new ArgumentException("COMPANY_CD is required", nameof(companyCd));
            }

            if (normalizedUserId.Length == 0)
            {
                throw new ArgumentException("USERID is required", nameof(request));
            }

            if (normalizedMenuCode.Length == 0)
            {
                throw new ArgumentException("MENU_CODE is required", nameof(request));
            }

            return new SysUserPermissionRequest
            {
                COMPANY_CD = companyCd,
                USERID = normalizedUserId,
                MENU_CODE = normalizedMenuCode,
                CAN_VIEW = Common.NormalizeFlagString(request.CAN_VIEW, "0"),
                CAN_ADD = Common.NormalizeFlagString(request.CAN_ADD, "0"),
                CAN_EDIT = Common.NormalizeFlagString(request.CAN_EDIT, "0"),
                CAN_DELETE = Common.NormalizeFlagString(request.CAN_DELETE, "0"),
                CAN_PRINT = Common.NormalizeFlagString(request.CAN_PRINT, "0"),
                CAN_EXPORT = Common.NormalizeFlagString(request.CAN_EXPORT, "0"),
                CAN_IMPORT = Common.NormalizeFlagString(request.CAN_IMPORT, "0"),
                CAN_APPROVE = Common.NormalizeFlagString(request.CAN_APPROVE, "0"),
                USERID_MODIFY = Common.NormalizeRequiredText(request.USERID_MODIFY)
            };
        }

        private static string BuildUserPermissionCacheKey(string userId)
        {
            return $"userId={userId}";
        }

        private static string? NormalizeSysCodeType(string? codeType)
        {
            return Common.NormalizeNullableText(codeType)?.ToUpperInvariant();
        }
    }
}
