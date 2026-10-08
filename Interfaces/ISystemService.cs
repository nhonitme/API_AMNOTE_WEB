using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISystemService
    {
        Task<IEnumerable<SysCodeInfo>> GetSysCodesAsync(string companyCd, string? codeType = null, bool refresh = false);

        Task<List<string>> GetExcelTemplateKeysAsync(string companyCd, string moduleCd);

        Task<List<ExcelTemplateColumnInfo>> GetExcelTemplateColumnInfosAsync(string companyCd, string moduleCd);

        Task<IEnumerable<EtcInfo>> GetEtcInfoAsync(string companyCd, string etcType, string lang, string param1, string param2);

        Task<IEnumerable<SysUserPermission>> GetUserPermissionsAsync(string companyCd, string userId, string? menuCode = null);

        Task<List<SysUserPermission>> RefreshUserPermissionsAsync(string companyCd, string userId);

        Task<int> SetUserPermissionAsync(string companyCd, SysUserPermissionRequest request);

        Task<int> ClearMasterDataCacheAsync(string scope, string? companyCd = null, bool global = false);

        Task<CacheClearSummary> ClearAllCacheAsync();
    }
}