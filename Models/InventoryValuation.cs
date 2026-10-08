namespace API_AMNOTE_WEB.Models
{
    public static class InventoryValuationMethod
    {
        public const string PeriodEndAverage = "PERIOD_END_AVG";
        public const string MovingAverage = "MOVING_AVG";
        public const string Fifo = "FIFO";
        public const string Specific = "SPECIFIC";

        public static string Normalize(string? methodCode)
        {
            var normalized = string.IsNullOrWhiteSpace(methodCode)
                ? PeriodEndAverage
                : methodCode.Trim().ToUpperInvariant();

            return normalized switch
            {
                PeriodEndAverage => PeriodEndAverage,
                MovingAverage => MovingAverage,
                Fifo => Fifo,
                Specific => Specific,
                _ => throw new ArgumentException($"Invalid METHOD_CODE: {methodCode}")
            };
        }

        public static string GetDisplayName(string methodCode)
        {
            return methodCode switch
            {
                PeriodEndAverage => "Bình quân cuối kỳ",
                MovingAverage => "Bình quân tức thời",
                Fifo => "Nhập trước xuất trước",
                Specific => "Giá đích danh",
                _ => methodCode
            };
        }
    }

    public class InventoryValuationRequest
    {
        public string? FROM_YMD { get; set; }
        public string? TO_YMD { get; set; }
        public string? PRODUCT_CDS { get; set; }
        public string? STORE_CDS { get; set; }
        public string? METHOD_CODE { get; set; }
    }

    public class InventoryValuationMovement
    {
        public string FLOW_TYPE { get; set; } = string.Empty;
        public string COMPANY_CD { get; set; } = string.Empty;
        public long? CHIT_ID { get; set; }
        public long? CHITDETAIL_ID { get; set; }
        public string CHIT_TYPE { get; set; } = string.Empty;
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string PRODUCT_NAME { get; set; } = string.Empty;
        public string STORE_CD { get; set; } = string.Empty;
        public string STORE_NAME { get; set; } = string.Empty;
        public string? TXN_YMD { get; set; }
        public string? DOCUMENT_NO { get; set; }
        public decimal QUANTITY { get; set; }
        public decimal UNIT_PRICE_CC { get; set; }
        public decimal AMOUNT_CC { get; set; }
        public string? STATE { get; set; }
    }

    public class InventoryValuationMethodResult
    {
        public string MethodCode { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public decimal InputQuantity { get; set; }
        public decimal InputAmount { get; set; }
        public decimal OutputQuantity { get; set; }
        public decimal OutputAmount { get; set; }
        public decimal EndingQuantity { get; set; }
        public decimal EndingAmount { get; set; }
        public decimal EndingAverageUnitCost { get; set; }
    }

    public class InventoryValuationCalculateResult
    {
        public string MethodCode { get; set; } = InventoryValuationMethod.PeriodEndAverage;
        public string MethodName { get; set; } = string.Empty;
        public int ProcessedGroups { get; set; }
        public int MovementCount { get; set; }
    }

    public static class InventoryValuationJobStatus
    {
        public const string Queued = BackgroundJobStatus.Queued;
        public const string Processing = BackgroundJobStatus.Processing;
        public const string Done = BackgroundJobStatus.Done;
        public const string Error = BackgroundJobStatus.Error;
    }

    public sealed class InventoryValuationJobRequest
    {
        public string JobId { get; init; } = Guid.NewGuid().ToString("N");
        public string CompanyCd { get; init; } = string.Empty;
        public string? DatabaseName { get; init; }
        public string FromYmd { get; init; } = string.Empty;
        public string ToYmd { get; init; } = string.Empty;
        public string MethodCode { get; init; } = string.Empty;
        public string? ProductCds { get; init; }
        public string? StoreCds { get; init; }
        public string? UserId { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.Now;
    }

    public sealed class InventoryValuationStartResultDto
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = InventoryValuationJobStatus.Queued;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class InventoryValuationProgressDto
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = InventoryValuationJobStatus.Queued;
        public int Percent { get; init; }
        public int ProcessedGroups { get; init; }
        public int TotalGroups { get; init; }
        public int MovementCount { get; init; }
        public string MethodCode { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;
        public string? Message { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public sealed class InventoryValuationJobState
    {
        public string JobId { get; init; } = string.Empty;
        public string CompanyCd { get; init; } = string.Empty;
        public string FromYmd { get; init; } = string.Empty;
        public string ToYmd { get; init; } = string.Empty;
        public string MethodCode { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;
        public string Status { get; set; } = InventoryValuationJobStatus.Queued;
        public int Percent { get; set; }
        public int ProcessedGroups { get; set; }
        public int TotalGroups { get; set; }
        public int MovementCount { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; init; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
