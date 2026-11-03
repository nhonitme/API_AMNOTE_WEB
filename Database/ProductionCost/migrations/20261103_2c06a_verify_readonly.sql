-- G-K 2C-06A — verify SELECT only (user tự chạy sau SOURCE DDL)
-- Không ghi dữ liệu.

SELECT
  COLUMN_NAME,
  COLUMN_TYPE,
  CHARACTER_MAXIMUM_LENGTH,
  IS_NULLABLE,
  COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND COLUMN_NAME IN ('OPERATION_ID', 'STABLE_KEY', 'STEP_CODE')
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

-- EXPECT: OPERATION_ID varchar(64), STABLE_KEY varchar(255),
--         uk_pc_hist_stable + uk_pc_hist_operation NON_UNIQUE=0

SELECT '2C-06A verify_readonly done' AS NOTE;
