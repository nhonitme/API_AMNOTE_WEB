-- =============================================================================
-- G-K 2C-03 — DDL nâng cấp UNIQUE WIP + DEPARTMENT_ID trên receipt
-- BÀN GIAO — user tự SOURCE trên DEV/TEST khi schema CŨ.
-- CẤM production. Agent không tự chạy.
--
-- ĐIỀU KIỆN:
--   CHẠY khi uk_pc_wip cũ = (RUN_ID, RUN_VERSION, PRODUCT_ID)
--        hoặc production_cost_receipt thiếu DEPARTMENT_ID.
--   KHÔNG CẦN khi cài mới từ 01_production_cost_tables.sql / 2b_01 bản repo hiện tại.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

-- -----------------------------------------------------------------------------
-- 0) Pre-check dữ liệu trước khi đổi UNIQUE WIP
-- -----------------------------------------------------------------------------
SELECT
    RUN_ID,
    RUN_VERSION,
    PRODUCT_ID,
    DEPARTMENT_ID,
    COUNT(*) AS CNT
FROM production_cost_wip
WHERE ISDEL = '0'
GROUP BY RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID
HAVING COUNT(*) > 1;

-- Nếu SELECT trên trả dòng → DỪNG, xử lý trùng thủ công trước khi ALTER.
-- (Khóa mới không cho phép trùng PRODUCT+DEPT trong cùng RUN_VERSION.)

SET @dup_new_key := (
  SELECT COUNT(*) FROM (
    SELECT 1
    FROM production_cost_wip
    WHERE ISDEL = '0'
    GROUP BY RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID
    HAVING COUNT(*) > 1
    LIMIT 1
  ) x
);

SET @stop_sql := IF(
  IFNULL(@dup_new_key, 0) = 0,
  'SELECT ''WIP precheck PASS — no duplicate PRODUCT+DEPT'' AS NOTE',
  'GK2C03_WIP_DUP_PRODUCT_DEPT__FIX_DATA_BEFORE_ALTER'
);
PREPARE stmt_pre FROM @stop_sql;
EXECUTE stmt_pre;
DEALLOCATE PREPARE stmt_pre;

-- -----------------------------------------------------------------------------
-- 1) Đổi UNIQUE production_cost_wip
-- -----------------------------------------------------------------------------
SET @uk_exists := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_wip'
    AND INDEX_NAME = 'uk_pc_wip'
);

SET @sql := IF(
  @uk_exists > 0,
  'ALTER TABLE `production_cost_wip` DROP INDEX `uk_pc_wip`',
  'SELECT ''uk_pc_wip absent'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

ALTER TABLE `production_cost_wip`
  ADD UNIQUE KEY `uk_pc_wip` (`RUN_ID`, `RUN_VERSION`, `PRODUCT_ID`, `DEPARTMENT_ID`);

-- -----------------------------------------------------------------------------
-- 2) DEPARTMENT_ID trên production_cost_receipt (giữ UNIQUE INPUT_ID)
-- -----------------------------------------------------------------------------
SET @col_exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_receipt'
    AND COLUMN_NAME = 'DEPARTMENT_ID'
);

SET @sql := IF(
  @col_exists = 0,
  'ALTER TABLE `production_cost_receipt`
     ADD COLUMN `DEPARTMENT_ID` bigint(20) NOT NULL DEFAULT 0
       COMMENT ''Khớp WIP PRODUCT+DEPT; 0 = chứng từ thiếu DEPT (khong tu suy doan)''
       AFTER `PRODUCT_ID`',
  'SELECT ''DEPARTMENT_ID already on receipt'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @idx_exists := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_receipt'
    AND INDEX_NAME = 'idx_pc_receipt_dept'
);

SET @sql := IF(
  @idx_exists = 0,
  'ALTER TABLE `production_cost_receipt`
     ADD KEY `idx_pc_receipt_dept` (`COMPANY_CD`, `PRODUCT_ID`, `DEPARTMENT_ID`)',
  'SELECT ''idx_pc_receipt_dept exists'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Xác nhận UNIQUE receipt vẫn theo INPUT_ID (không đổi)
SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_receipt'
  AND INDEX_NAME = 'uk_pc_receipt'
GROUP BY INDEX_NAME;

SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_wip'
  AND INDEX_NAME = 'uk_pc_wip'
GROUP BY INDEX_NAME;

SELECT '2C-03 DDL wip/receipt key applied' AS NOTE;
