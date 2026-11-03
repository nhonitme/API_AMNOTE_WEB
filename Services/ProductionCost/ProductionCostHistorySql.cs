namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// SQL đọc history — tách schema cũ (trước 2C-06A) / mới (có OPERATION_ID…).
/// Unit-testable không cần DB.
/// </summary>
public static class ProductionCostHistorySql
{
    public const string LegacySelect = @"
SELECT
    HISTORY_ID AS HistoryId,
    RUN_ID AS RunId,
    PERIOD_YM AS PeriodYm,
    STEP_INDEX AS StepIndex,
    ACTION_CODE AS ActionCode,
    STATUS_AFTER AS StatusAfter,
    RUN_VERSION AS RunVersion,
    RESULT_BEFORE AS ResultBefore,
    RESULT_AFTER AS ResultAfter,
    DIFFERENCE_TEXT AS DifferenceText,
    REASON AS Reason,
    USER_NAME AS UserName,
    ACTION_AT AS ActionAt
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND PERIOD_YM = @PeriodYm
  AND ISDEL = '0'
  AND (@RunId IS NULL OR RUN_ID = @RunId)
ORDER BY ACTION_AT DESC, HISTORY_ID DESC";

    public const string IdempotencySelect = @"
SELECT
    HISTORY_ID AS HistoryId,
    RUN_ID AS RunId,
    PERIOD_YM AS PeriodYm,
    STEP_INDEX AS StepIndex,
    STEP_CODE AS StepCode,
    ACTION_CODE AS ActionCode,
    OPERATION_ID AS OperationId,
    STABLE_KEY AS StableKey,
    CONTENT_FINGERPRINT AS ContentFingerprint,
    STATUS_AFTER AS StatusAfter,
    RUN_VERSION AS RunVersion,
    RESULT_BEFORE AS ResultBefore,
    RESULT_AFTER AS ResultAfter,
    DIFFERENCE_TEXT AS DifferenceText,
    REASON AS Reason,
    USER_NAME AS UserName,
    ACTION_AT AS ActionAt
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND PERIOD_YM = @PeriodYm
  AND ISDEL = '0'
  AND (@RunId IS NULL OR RUN_ID = @RunId)
ORDER BY ACTION_AT DESC, HISTORY_ID DESC";

    public const string ProbeIdempotencyColumns = @"
SELECT COUNT(*)
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND COLUMN_NAME IN ('OPERATION_ID', 'STABLE_KEY', 'STEP_CODE', 'CONTENT_FINGERPRINT')";

    public static string SelectHistory(bool hasIdempotencyColumns) =>
        hasIdempotencyColumns ? IdempotencySelect : LegacySelect;

    /// <summary>Đủ 4 cột idempotency 2C-06A (kể cả CONTENT_FINGERPRINT).</summary>
    public static bool ProbeCountMeansReady(int columnCount) => columnCount >= 4;
}
