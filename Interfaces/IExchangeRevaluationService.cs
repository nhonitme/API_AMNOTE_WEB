using API_AMNOTE_WEB.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IExchangeRevaluationService
    {
        Task<ExchangeRevaluationSaveResult> SaveAsync(
            string companyCd,
            string userId,
            string databaseName,
            string? moduleCd,
            DateTime rateDate,
            string? fcType,
            string? chitYmdFrom,
            string? chitYmdTo,
            Action<int, int, string?>? onProgress = null,
            CancellationToken cancellationToken = default);
    }
}
