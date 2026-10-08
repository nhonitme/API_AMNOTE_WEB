using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class ExchangeRevaluationService : IExchangeRevaluationService
    {
        private readonly IExchangeRevaluationRepository _repository;
        private readonly ILogger<ExchangeRevaluationService> _logger;

        public ExchangeRevaluationService(
            IExchangeRevaluationRepository repository,
            ILogger<ExchangeRevaluationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<ExchangeRevaluationSaveResult> SaveAsync(
            string companyCd,
            string userId,
            string databaseName,
            string? moduleCd,
            DateTime rateDate,
            string? fcType,
            string? chitYmdFrom,
            string? chitYmdTo,
            Action<int, int, string?>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new ArgumentException("COMPANY_CD is required");
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("DATABASE_NAME is required");
            }

            cancellationToken.ThrowIfCancellationRequested();
            onProgress?.Invoke(0, 100, "Đang tính lại tỷ giá ngân hàng...");

            var result = await _repository.SaveAsync(
                companyCd,
                userId,
                moduleCd,
                rateDate,
                databaseName,
                fcType,
                chitYmdFrom,
                chitYmdTo);

            cancellationToken.ThrowIfCancellationRequested();
            onProgress?.Invoke(100, 100, $"Hoàn tất ({result.SAVED_COUNT} dòng).");

            _logger.LogInformation(
                "Exchange revaluation saved for company {CompanyCd}, module {ModuleCd}, savedCount={SavedCount}",
                companyCd,
                moduleCd,
                result.SAVED_COUNT);

            return result;
        }
    }
}
