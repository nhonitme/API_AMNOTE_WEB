namespace API_AMNOTE_WEB.Models
{
    public interface IExchangeRevaluationAmountInfo
    {
        decimal OLD_AMOUNT { get; }
        decimal? NEW_AMOUNT { get; }
        decimal? EXCHANGE_DIFF { get; }
    }

    public class ExchangeRevaluationPreviewRequest
    {
        public string? MODULE_CD { get; set; }
        public string? RATE_DATE { get; set; }
        public string? FC_TYPE { get; set; }
        public string? CHIT_YMD_FROM { get; set; }
        public string? CHIT_YMD_TO { get; set; }
        public string? RATE_METHOD { get; set; }
    }

    public class ExchangeRevaluationHistoryRequest
    {
        public string? MODULE_CD { get; set; }
        public string? RATE_DATE_FROM { get; set; }
        public string? RATE_DATE_TO { get; set; }
        public string? FC_TYPE { get; set; }
    }

    public class ExchangeRevaluationPreviewItem : IExchangeRevaluationAmountInfo
    {
        public string MODULE_CD { get; set; } = string.Empty;
        public string COMPANY_CD { get; set; } = string.Empty;
        public long CHIT_ID { get; set; }
        public long CHITDETAIL_ID { get; set; }
        public string? CHIT_CD { get; set; }
        public string? CHIT_NO { get; set; }
        public string? CHIT_YMD { get; set; }
        public string? CHIT_TYPE { get; set; }
        public string? CHITDETAIL_CD { get; set; }
        public string? DEBIT { get; set; }
        public string? CREDIT { get; set; }
        public string? FC_TYPE { get; set; }
        public decimal FC_AMOUNT { get; set; }
        public decimal OLD_RATE { get; set; }
        public decimal OLD_AMOUNT { get; set; }
        public decimal? NEW_RATE { get; set; }
        public decimal? NEW_AMOUNT { get; set; }
        public decimal? EXCHANGE_DIFF { get; set; }
        public string DIFF_TYPE { get; set; } = "NONE";
    }

    public class ExchangeRevaluationHistoryItem : IExchangeRevaluationAmountInfo
    {
        public long RESULT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string MODULE_CD { get; set; } = string.Empty;
        public long CHIT_ID { get; set; }
        public long CHITDETAIL_ID { get; set; }
        public string? CHIT_CD { get; set; }
        public string? CHIT_NO { get; set; }
        public string? CHIT_YMD { get; set; }
        public string? CHIT_TYPE { get; set; }
        public string? CHITDETAIL_CD { get; set; }
        public string? DEBIT { get; set; }
        public string? CREDIT { get; set; }
        public string? FC_TYPE { get; set; }
        public string? RATE_DATE { get; set; }
        public decimal FC_AMOUNT { get; set; }
        public decimal OLD_RATE { get; set; }
        public decimal NEW_RATE { get; set; }
        public decimal OLD_AMOUNT { get; set; }
        public decimal NEW_AMOUNT { get; set; }
        public decimal EXCHANGE_DIFF { get; set; }
        public string DIFF_TYPE { get; set; } = "NONE";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }

        decimal IExchangeRevaluationAmountInfo.OLD_AMOUNT => OLD_AMOUNT;
        decimal? IExchangeRevaluationAmountInfo.NEW_AMOUNT => NEW_AMOUNT;
        decimal? IExchangeRevaluationAmountInfo.EXCHANGE_DIFF => EXCHANGE_DIFF;
    }

    public class ExchangeRevaluationCurrencyItem
    {
        public string FC_TYPE { get; set; } = string.Empty;
    }

    public class ExchangeRevaluationSummary
    {
        public int TOTAL_ROWS { get; set; }
        public decimal TOTAL_OLD_AMOUNT { get; set; }
        public decimal TOTAL_NEW_AMOUNT { get; set; }
        public decimal TOTAL_EXCHANGE_DIFF { get; set; }
        public decimal TOTAL_GAIN { get; set; }
        public decimal TOTAL_LOSS { get; set; }
    }

    public class ExchangeRevaluationSaveResult
    {
        public int SAVED_COUNT { get; set; }
        public List<ExchangeRevaluationHistoryItem> ROWS { get; set; } = new List<ExchangeRevaluationHistoryItem>();
    }

    public sealed class CashExchangeRevaluationJobRequest
    {
        public string JobId { get; init; } = Guid.NewGuid().ToString("N");
        public string CompanyCd { get; init; } = string.Empty;
        public string? DatabaseName { get; init; }
        public string UserId { get; init; } = string.Empty;
        public DateTime RateDate { get; init; }
        public string? FcType { get; init; }
        public string? ChitYmdFrom { get; init; }
        public string? ChitYmdTo { get; init; }
        public string? RateMethod { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.Now;
    }

    public sealed class CashExchangeRevaluationStartResultDto
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = BackgroundJobStatus.Queued;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class CashExchangeRevaluationProgressDto
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = BackgroundJobStatus.Queued;
        public int Percent { get; init; }
        public int SavedCount { get; init; }
        public string? Message { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public sealed class CashExchangeRevaluationJobState
    {
        public string JobId { get; init; } = string.Empty;
        public string CompanyCd { get; init; } = string.Empty;
        public string Status { get; set; } = BackgroundJobStatus.Queued;
        public int Percent { get; set; }
        public int SavedCount { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class ExchangeRevaluationJobRequest
    {
        public string JobId { get; init; } = Guid.NewGuid().ToString("N");
        public string CompanyCd { get; init; } = string.Empty;
        public string? DatabaseName { get; init; }
        public string UserId { get; init; } = string.Empty;
        public string? ModuleCd { get; init; }
        public DateTime RateDate { get; init; }
        public string? FcType { get; init; }
        public string? ChitYmdFrom { get; init; }
        public string? ChitYmdTo { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.Now;
    }

    public sealed class ExchangeRevaluationStartResultDto
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = BackgroundJobStatus.Queued;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class ExchangeRevaluationProgressDto
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = BackgroundJobStatus.Queued;
        public int Percent { get; init; }
        public int SavedCount { get; init; }
        public string? Message { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public sealed class ExchangeRevaluationJobState
    {
        public string JobId { get; init; } = string.Empty;
        public string CompanyCd { get; init; } = string.Empty;
        public string Status { get; set; } = BackgroundJobStatus.Queued;
        public int Percent { get; set; }
        public int SavedCount { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; set; }
    }
}
