using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICashExchangeRevaluationRepository
    {
        Task<IEnumerable<ExchangeRevaluationPreviewItem>> PreviewAsync(
            string companyCd,
            DateTime rateDate,
            string? databaseName = null,
            string? fcType = null,
            string? chitYmdFrom = null,
            string? chitYmdTo = null,
            string? rateMethod = null);

        Task<ExchangeRevaluationSaveResult> SaveAsync(
            string companyCd,
            string userId,
            DateTime rateDate,
            string? databaseName = null,
            string? fcType = null,
            string? chitYmdFrom = null,
            string? chitYmdTo = null,
            string? rateMethod = null);

        Task<IEnumerable<ExchangeRevaluationHistoryItem>> GetHistoryAsync(
            string companyCd,
            DateTime? rateDateFrom = null,
            DateTime? rateDateTo = null,
            string? fcType = null,
            string? databaseName = null);

        Task<IEnumerable<ExchangeRevaluationCurrencyItem>> GetCurrenciesAsync(
            string companyCd,
            string? databaseName = null);
    }
}
