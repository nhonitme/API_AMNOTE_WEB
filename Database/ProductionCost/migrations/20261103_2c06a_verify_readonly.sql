-- G-K 2C-06A — verify SELECT only (user tự chạy sau SOURCE DDL)

SELECT
  COLUMN_NAME,
  COLUMN_TYPE,
  CHARACTER_MAXIMUM_LENGTH,
  IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND COLUMN_NAME IN ('OPERATION_ID', 'STABLE_KEY', 'STEP_CODE', 'CONTENT_FINGERPRINT')
ORDER BY ORDINAL_POSITION;

SELECT
  INDEX_NAME,
  NON_UNIQUE,
  GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') AS COLS
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND INDEX_NAME IN ('uk_pc_hist_stable', 'uk_pc_hist_operation')
GROUP BY INDEX_NAME, NON_UNIQUE
ORDER BY INDEX_NAME;

-- EXPECT:
--   OPERATION_ID varchar(64), STABLE_KEY varchar(190), CONTENT_FINGERPRINT varchar(64)
--   uk_pc_hist_stable / uk_pc_hist_operation NON_UNIQUE=0 (full columns, no prefix)
--   utf8 index bytes: (20+190)*3=630 ≤ 767

SELECT '2C-06A verify_readonly done' AS NOTE;
