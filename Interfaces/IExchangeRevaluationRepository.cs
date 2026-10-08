using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IExchangeRevaluationRepository
    {
        Task<IEnumerable<ExchangeRevaluationPreviewItem>> PreviewAsync(
            string companyCd,
            string? moduleCd,
            DateTime rateDate,
            string? databaseName = null,
            string? fcType = null,
            string? chitYmdFrom = null,
            string? chitYmdTo = null);

        Task<ExchangeRevaluationSaveResult> SaveAsync(
            string companyCd,
            string userId,
            string? moduleCd,
            DateTime rateDate,
            string? databaseName = null,
            string? fcType = null,
            string? chitYmdFrom = null,
            string? chitYmdTo = null);

        Task<IEnumerable<ExchangeRevaluationHistoryItem>> GetHistoryAsync(
            string companyCd,
            string? moduleCd = null,
            DateTime? rateDateFrom = null,
            DateTime? rateDateTo = null,
            string? fcType = null,
            string? databaseName = null);

        Task<IEnumerable<ExchangeRevaluationCurrencyItem>> GetCurrenciesAsync(
            string companyCd,
            string? moduleCd = null,
            string? databaseName = null);
    }
}
