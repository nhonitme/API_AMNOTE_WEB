using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISystemRepository
    {
        Task<IEnumerable<SysCodeInfo>> GetSysCodeAsync(string? codeType);
        Task<List<ExcelTemplateColumnInfo>> GetExcelTemplateColumnInfosAsync(string companyCd, string moduleCd);
        Task<IEnumerable<SysUserPermission>> GetUserPermissionsAsync(string companyCd, string userId);
        Task<int> SetUserPermissionAsync(DapperSession session, string companyCd, SysUserPermissionRequest request);
        Task<IEnumerable<EtcInfo>> GetEtcInfoAsync(string companyCd, string etcType, string lang, string param1, string param2, string? databaseName = null);
    }
}
