using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class ExchangeRevaluationRepository : IExchangeRevaluationRepository
    {
        private readonly DapperExecutor _db;

        public ExchangeRevaluationRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IEnumerable<ExchangeRevaluationPreviewItem>> PreviewAsync(
            string companyCd,
            string? moduleCd,
            DateTime rateDate,
            string? databaseName = null,
            string? fcType = null,
            string? chitYmdFrom = null,
            string? chitYmdTo = null)
        {
            const string query = @"CALL get_exchange_revaluation_preview(
                @p_COMPANY_CD,
                @p_MODULE_CD,
                @p_RATE_DATE,
                @p_FC_TYPE,
                @p_CHIT_YMD_FROM,
                @p_CHIT_YMD_TO
            )";

            return await _db.QueryAsync<ExchangeRevaluationPreviewItem>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MODULE_CD = moduleCd,
                p_RATE_DATE = rateDate.Date,
                p_FC_TYPE = fcType,
                p_CHIT_YMD_FROM = chitYmdFrom,
                p_CHIT_YMD_TO = chitYmdTo
            }, databaseName);
        }

        public async Task<ExchangeRevaluationSaveResult> SaveAsync(
            string companyCd,
            string userId,
            string? moduleCd,
            DateTime rateDate,
            string? databaseName = null,
            string? fcType = null,
            string? chitYmdFrom = null,
            string? chitYmdTo = null)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);

            try
            {
                const string saveQuery = @"CALL save_exchange_revaluation_result(
                    @p_COMPANY_CD,
                    @p_MODULE_CD,
                    @p_RATE_DATE,
                    @p_FC_TYPE,
                    @p_CHIT_YMD_FROM,
                    @p_CHIT_YMD_TO,
                    @p_CREATE_BY
                )";

                var rows = (await session.QueryAsync<ExchangeRevaluationHistoryItem>(saveQuery, new
                {
                    p_COMPANY_CD = companyCd,
                    p_MODULE_CD = moduleCd,
                    p_RATE_DATE = rateDate.Date,
                    p_FC_TYPE = fcType,
                    p_CHIT_YMD_FROM = chitYmdFrom,
                    p_CHIT_YMD_TO = chitYmdTo,
                    p_CREATE_BY = userId
                })).ToList();

                session.Commit();

                return new ExchangeRevaluationSaveResult
                {
                    SAVED_COUNT = rows.Count,
                    ROWS = rows
                };
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<ExchangeRevaluationHistoryItem>> GetHistoryAsync(
            string companyCd,
            string? moduleCd = null,
            DateTime? rateDateFrom = null,
            DateTime? rateDateTo = null,
            string? fcType = null,
            string? databaseName = null)
        {
            const string query = @"CALL get_exchange_revaluation_history(
                @p_COMPANY_CD,
                @p_MODULE_CD,
                @p_RATE_DATE_FROM,
                @p_RATE_DATE_TO,
                @p_FC_TYPE
            )";

            return await _db.QueryAsync<ExchangeRevaluationHistoryItem>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MODULE_CD = moduleCd,
                p_RATE_DATE_FROM = rateDateFrom?.Date,
                p_RATE_DATE_TO = rateDateTo?.Date,
                p_FC_TYPE = fcType
            }, databaseName);
        }

        public async Task<IEnumerable<ExchangeRevaluationCurrencyItem>> GetCurrenciesAsync(
            string companyCd,
            string? moduleCd = null,
            string? databaseName = null)
        {
            const string query = @"CALL get_exchange_revaluation_currency_lookup(
                @p_COMPANY_CD,
                @p_MODULE_CD
            )";

            return await _db.QueryAsync<ExchangeRevaluationCurrencyItem>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_MODULE_CD = moduleCd
            }, databaseName);
        }
    }
}
