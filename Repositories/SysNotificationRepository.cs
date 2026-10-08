using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class SysNotificationRepository : ISysNotificationRepository
    {
        private readonly DapperExecutor _db;
        private readonly ILogger<SysNotificationRepository> _logger;

        public SysNotificationRepository(DapperExecutor db, ILogger<SysNotificationRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<SysNotificationListResult> GetListAsync(
            string companyCd,
            string? userId,
            string? roleCd,
            SysNotificationQueryDto query)
        {
            const string sql = "CALL getSysNotification(@p_COMPANY_CD, @p_USER_ID, @p_ROLE_CD, @p_IS_READ, @p_NOTIFICATION_TYPE, @p_SOURCE_MODULE, @p_KEYWORD, @p_PAGE_NUMBER, @p_PAGE_SIZE)";

            using var conn = _db.GetOpenConnection(Net_DB.Net_DB_Company);
            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_ID = userId,
                p_ROLE_CD = roleCd,
                p_IS_READ = Common.NormalizeNullableText(query.IsRead),
                p_NOTIFICATION_TYPE = Common.NormalizeNullableText(query.NotificationType),
                p_SOURCE_MODULE = Common.NormalizeNullableText(query.SourceModule),
                p_KEYWORD = Common.NormalizeNullableText(query.Keyword),
                p_PAGE_NUMBER = query.PageNumber,
                p_PAGE_SIZE = query.PageSize
            });

            var totalRow = await multi.ReadFirstOrDefaultAsync<dynamic>();
            var items = (await multi.ReadAsync<SysNotification>()).ToList();

            return new SysNotificationListResult
            {
                TotalCount = (int)(totalRow?.TOTAL_COUNT ?? 0),
                Items = items
            };
        }

        public async Task<SysNotificationSummary?> GetSummaryAsync(string companyCd, string? userId, string? roleCd)
        {
            const string sql = "CALL getSysNotificationSummary(@p_COMPANY_CD, @p_USER_ID, @p_ROLE_CD)";
            var rows = await _db.QueryAsync<SysNotificationSummary>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_ID = userId,
                p_ROLE_CD = roleCd
            });

            return rows.FirstOrDefault();
        }

        public async Task<long> SaveAsync(string companyCd, string createdBy, SysNotificationCreateRequest request, long? id = null)
        {
            const string sql = @"CALL setSysNotification(
                @p_ID,
                @p_COMPANY_CD,
                @p_USER_ID,
                @p_ROLE_CD,
                @p_NOTIFICATION_TYPE,
                @p_SOURCE_MODULE,
                @p_SOURCE_ID,
                @p_TITLE,
                @p_MESSAGE,
                @p_PRIORITY,
                @p_ACTION_URL,
                @p_IS_READ,
                @p_EXPIRED_AT,
                @p_CREATE_BY)";

            var rows = await _db.QueryAsync<dynamic>(Net_DB.Net_DB_Company, sql, new
            {
                p_ID = id ?? 0,
                p_COMPANY_CD = companyCd,
                p_USER_ID = Common.NormalizeNullableText(request.UserId),
                p_ROLE_CD = Common.NormalizeNullableText(request.RoleCd),
                p_NOTIFICATION_TYPE = request.NotificationType.Trim(),
                p_SOURCE_MODULE = Common.NormalizeNullableText(request.SourceModule),
                p_SOURCE_ID = Common.NormalizeNullableText(request.SourceId),
                p_TITLE = request.Title.Trim(),
                p_MESSAGE = Common.NormalizeNullableText(request.Message),
                p_PRIORITY = string.IsNullOrWhiteSpace(request.Priority) ? "MEDIUM" : request.Priority.Trim().ToUpperInvariant(),
                p_ACTION_URL = Common.NormalizeNullableText(request.ActionUrl),
                p_IS_READ = "N",
                p_EXPIRED_AT = request.ExpiredAt,
                p_CREATE_BY = createdBy
            });

            var row = rows.FirstOrDefault();
            return (long)(row?.ID ?? 0);
        }

        public async Task<int> MarkReadAsync(string companyCd, string? userId, string? roleCd, long? id, bool markAll)
        {
            const string sql = "CALL markSysNotificationRead(@p_COMPANY_CD, @p_USER_ID, @p_ROLE_CD, @p_ID, @p_MARK_ALL)";
            var rows = await _db.QueryAsync<dynamic>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_ID = userId,
                p_ROLE_CD = roleCd,
                p_ID = id ?? 0,
                p_MARK_ALL = markAll ? "Y" : "N"
            });

            var row = rows.FirstOrDefault();
            return (int)(row?.AFFECTED_ROWS ?? 0);
        }

        public async Task<int> SyncFromRulesAsync(string companyCd, string? userId, string fromYmd, string toYmd)
        {
            const string sql = "CALL syncSysNotificationFromRules(@p_COMPANY_CD, @p_USER_ID, @p_FROM_YMD, @p_TO_YMD)";
            var rows = await _db.QueryAsync<dynamic>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_ID = userId,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd
            });

            var row = rows.FirstOrDefault();
            var syncedCount = (int)(row?.SYNCED_COUNT ?? 0);
            _logger.LogInformation(
                "Synced notifications from rules. CompanyCd={CompanyCd}, UserId={UserId}, SyncedCount={SyncedCount}",
                companyCd,
                userId,
                syncedCount);

            return syncedCount;
        }
    }
}
