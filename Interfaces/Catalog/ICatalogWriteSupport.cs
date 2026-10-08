using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces.Catalog
{
    public interface ICatalogWriteSupport
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<DapperSession, Task<T>> work, string? databaseName = null);

        Task<string> ResolveUpsertCodeByContextAsync(
            DapperSession session,
            string companyCd,
            string menuCode,
            string codeField,
            string? existingCode,
            string? requestedCode,
            string fieldName,
            DateTime? baseDate = null);

        Task LogUpsertAsync(
            DapperSession session,
            string companyCd,
            bool isInsert,
            string moduleName,
            string tableName,
            string recordId,
            string? oldData,
            object newData,
            string description);

        Task LogInsertAsync(
            DapperSession session,
            string companyCd,
            string moduleName,
            string tableName,
            string recordId,
            object newData,
            string description);

        Task<int> DeleteBatchAsync(
            DapperSession session,
            string companyCd,
            IEnumerable<long> ids,
            Func<DapperSession, string, long, Task<int>> deleteSingle,
            IReadOnlyDictionary<long, DeleteActivityLogEntry>? auditEntries,
            string moduleName,
            string tableName,
            string description);
    }
}
