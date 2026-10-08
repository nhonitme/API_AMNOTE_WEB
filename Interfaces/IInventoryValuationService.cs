using API_AMNOTE_WEB.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IInventoryValuationService
    {
        Task<InventoryValuationCalculateResult> CalculateAsync(
            string companyCd,
            string fromYmd,
            string toYmd,
            string methodCode,
            string? databaseName = null,
            string? productCds = null,
            string? storeCds = null,
            Action<int, int, int, string?>? onProgress = null,
            CancellationToken cancellationToken = default);
    }
}
