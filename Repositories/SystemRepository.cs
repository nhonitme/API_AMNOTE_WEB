using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class SystemRepository : ISystemRepository
    {
        private readonly DapperExecutor _db;

        public SystemRepository(DapperExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IEnumerable<SysCodeInfo>> GetSysCodeAsync(string? codeType)
        {
            const string query = "CALL get_sys_code(@p_CODE_TYPE)";
            return await _db.QueryAsync<SysCodeInfo>(Net_DB.Net_DB_Manager, query, new { p_CODE_TYPE = codeType });
        }

        public async Task<List<ExcelTemplateColumnInfo>> GetExcelTemplateColumnInfosAsync(string companyCd, string moduleCd)
        {
            const string query = "CALL getexcel_template_keys(@p_COMPANY_CD, @p_MODULE_CD)";
            var result = await _db.QueryAsync<ExcelTemplateColumnInfo>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MODULE_CD = moduleCd
            });

            return result?.ToList() ?? new List<ExcelTemplateColumnInfo>();
        }

        public async Task<IEnumerable<SysUserPermission>> GetUserPermissionsAsync(string companyCd, string userId)
        {
            const string query = "CALL get_sys_user_permission(@p_COMPANY_CD, @p_USERID)";
            return await _db.QueryAsync<SysUserPermission>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_CD = companyCd,
                p_USERID = userId
            });
        }

        public async Task<int> SetUserPermissionAsync(DapperSession session, string companyCd, SysUserPermissionRequest request)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            const string query = @"CALL set_sys_user_permission(
                @p_COMPANY_CD,
                @p_USERID,
                @p_MENU_CODE,
                @p_CAN_VIEW,
                @p_CAN_ADD,
                @p_CAN_EDIT,
                @p_CAN_DELETE,
                @p_CAN_PRINT,
                @p_CAN_EXPORT,
                @p_CAN_IMPORT,
                @p_CAN_APPROVE,
                @p_USERID_MODIFY
            )";

            return await session.ExecuteAsync(query, new
            {
                p_COMPANY_CD = companyCd,
                p_USERID = request.USERID,
                p_MENU_CODE = request.MENU_CODE,
                p_CAN_VIEW = request.CAN_VIEW,
                p_CAN_ADD = request.CAN_ADD,
                p_CAN_EDIT = request.CAN_EDIT,
                p_CAN_DELETE = request.CAN_DELETE,
                p_CAN_PRINT = request.CAN_PRINT,
                p_CAN_EXPORT = request.CAN_EXPORT,
                p_CAN_IMPORT = request.CAN_IMPORT,
                p_CAN_APPROVE = request.CAN_APPROVE,
                p_USERID_MODIFY = request.USERID_MODIFY
            });
        }

        public async Task<IEnumerable<EtcInfo>> GetEtcInfoAsync(
            string companyCd,
            string etcType,
            string lang,
            string param1,
            string param2,
            string? databaseName = null)
        {
            const string query = "CALL getEtcData_info(@p_COMPANY_CD,@p_etcType,@p_lang,@p_param1,@p_param2)";
            return await _db.QueryAsync<EtcInfo>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_etcType = etcType,
                    p_lang = lang,
                    p_param1 = param1,
                    p_param2 = param2
                },
                databaseName);
        }
    }
}