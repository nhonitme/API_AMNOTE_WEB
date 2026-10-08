using API_AMNOTE_WEB.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IInventoryValuationRepository
    {
        Task<IReadOnlyList<InventoryValuationMovement>> GetMovementsAsync(
            string companyCd,
            string fromYmd,
            string toYmd,
            string? databaseName = null,
            string? productCds = null,
            string? storeCds = null);
    }
}
