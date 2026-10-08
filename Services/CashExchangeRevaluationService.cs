using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Logging;

namespace API_AMNOTE_WEB.Services
{
    public class CashExchangeRevaluationService : ICashExchangeRevaluationService
    {
        private readonly ICashExchangeRevaluationRepository _repository;
        private readonly ILogger<CashExchangeRevaluationService> _logger;

        public CashExchangeRevaluationService(
            ICashExchangeRevaluationRepository repository,
            ILogger<CashExchangeRevaluationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<ExchangeRevaluationSaveResult> SaveAsync(
            string companyCd,
            string userId,
            string databaseName,
            DateTime rateDate,
            string? fcType,
            string? chitYmdFrom,
            string? chitYmdTo,
            string? rateMethod,
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
            onProgress?.Invoke(0, 100, "Đang tính lại tỷ giá xuất quỹ...");

            var result = await _repository.SaveAsync(
                companyCd,
                userId,
                rateDate,
                databaseName,
                fcType,
                chitYmdFrom,
                chitYmdTo,
                rateMethod);

            cancellationToken.ThrowIfCancellationRequested();
            onProgress?.Invoke(100, 100, $"Hoàn tất ({result.SAVED_COUNT} dòng).");

            _logger.LogInformation(
                "Cash exchange revaluation saved for company {CompanyCd}, savedCount={SavedCount}",
                companyCd,
                result.SAVED_COUNT);

            return result;
        }
    }
}
