using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class CatalogWriteSupport : ICatalogWriteSupport
    {
        private readonly DapperExecutor _db;
        private readonly ISysCodeSequenceRepository _sequenceRepository;
        private readonly IActivityLogService _activityLogService;

        public CatalogWriteSupport(
            DapperExecutor db,
            ISysCodeSequenceRepository sequenceRepository,
            IActivityLogService activityLogService)
        {
            _db = db;
            _sequenceRepository = sequenceRepository;
            _activityLogService = activityLogService;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<DapperSession, Task<T>> work, string? databaseName = null)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);
            try
            {
                var result = await work(session);
                session.Commit();
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<string> ResolveUpsertCodeByContextAsync(
            DapperSession session,
            string companyCd,
            string menuCode,
            string codeField,
            string? existingCode,
            string? requestedCode,
            string fieldName,
            DateTime? baseDate = null)
        {
            var normalizedExisting = Common.NormalizeNullableText(existingCode);
            var normalizedRequest = Common.NormalizeNullableText(requestedCode);

            if (!string.IsNullOrWhiteSpace(normalizedExisting) && string.IsNullOrWhiteSpace(normalizedRequest))
                return normalizedExisting;

            if (!string.IsNullOrWhiteSpace(normalizedExisting) &&
                !string.IsNullOrWhiteSpace(normalizedRequest) &&
                string.Equals(normalizedExisting, normalizedRequest, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedRequest;
            }

            return await CodeSequenceHelper.ResolveRequiredCodeByContextAsync(
                _sequenceRepository,
                session,
                companyCd,
                menuCode,
                codeField,
                normalizedRequest,
                fieldName,
                baseDate);
        }

        public Task LogUpsertAsync(
            DapperSession session,
            string companyCd,
            bool isInsert,
            string moduleName,
            string tableName,
            string recordId,
            string? oldData,
            object newData,
            string description)
        {
            return _activityLogService.LogAsync(
                session.Connection,
                session.Transaction,
                companyCd,
                isInsert ? "INSERT" : "UPDATE",
                moduleName,
                tableName,
                recordId,
                oldData ?? string.Empty,
                JsonSerializer.Serialize(newData),
                description);
        }

        public Task LogInsertAsync(
            DapperSession session,
            string companyCd,
            string moduleName,
            string tableName,
            string recordId,
            object newData,
            string description)
        {
            return _activityLogService.LogAsync(
                session.Connection,
                session.Transaction,
                companyCd,
                "INSERT",
                moduleName,
                tableName,
                recordId,
                string.Empty,
                JsonSerializer.Serialize(newData),
                description);
        }

        public async Task<int> DeleteBatchAsync(
            DapperSession session,
            string companyCd,
            IEnumerable<long> ids,
            Func<DapperSession, string, long, Task<int>> deleteSingle,
            IReadOnlyDictionary<long, DeleteActivityLogEntry>? auditEntries,
            string moduleName,
            string tableName,
            string description)
        {
            var affectedRows = 0;
            foreach (var id in ids.Distinct())
            {
                if (id <= 0)
                    continue;

                var result = await deleteSingle(session, companyCd, id);
                if (result <= 0)
                    continue;

                affectedRows += result;
                if (auditEntries != null && auditEntries.TryGetValue(id, out var audit))
                {
                    await DeleteActivityLogHelper.LogDeleteAsync(
                        _activityLogService,
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        moduleName,
                        tableName,
                        audit,
                        description);
                }
            }

            return affectedRows;
        }
    }
}
