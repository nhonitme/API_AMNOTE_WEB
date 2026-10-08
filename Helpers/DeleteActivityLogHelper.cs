using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Data;

namespace API_AMNOTE_WEB.Helpers
{
    public static class DeleteActivityLogHelper
    {
        public static Task LogDeleteAsync(
            IActivityLogService activityLogService,
            IDbConnection connection,
            IDbTransaction? transaction,
            string companyCd,
            string moduleName,
            string tableName,
            DeleteActivityLogEntry entry,
            string description)
        {
            return activityLogService.LogAsync(
                connection,
                transaction,
                companyCd,
                "DELETE",
                moduleName,
                tableName,
                entry.RecordId,
                entry.OldData,
                string.Empty,
                description);
        }
    }
}
