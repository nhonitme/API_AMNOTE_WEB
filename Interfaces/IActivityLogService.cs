using System.Data;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IActivityLogService
    {
        Task LogAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction? transaction, string companyCd, string actionType, string moduleName, string tableName, string recordId, string oldData, string newData, string description);
    }
}
