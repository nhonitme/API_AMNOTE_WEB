-- =============================================================================
-- G-K 2C-06A — DDL: OPERATION_ID / STABLE_KEY / STEP_CODE trên production_cost_history
-- BÀN GIAO — user tự SOURCE trên DEV/TEST. Agent không tự chạy.
-- CẤM production. Không bật EnableWrites trong bước SOURCE này.
--
-- MySQL 5.6 / 8.0: information_schema + PREPARE (không JSON typed / không IF NOT EXISTS cột).
--
-- ĐIỀU KIỆN:
--   CHẠY khi bảng production_cost_history đã tồn tại (2B) và thiếu cột idempotency.
--   KHÔNG CẦN khi cài mới từ 01_production_cost_tables.sql / 2b_01 bản repo đã có cột.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

-- -----------------------------------------------------------------------------
-- 0) Bảng phải tồn tại
-- -----------------------------------------------------------------------------
SET @tbl_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_history'
);

SET @stop_tbl := IF(
  IFNULL(@tbl_exists, 0) = 1,
  'SELECT ''production_cost_history present'' AS NOTE',
  'GK2C06A_HISTORY_TABLE_MISSING__INSTALL_2B_FIRST'
);
PREPARE stmt_tbl FROM @stop_tbl;
EXECUTE stmt_tbl;
DEALLOCATE PREPARE stmt_tbl;

-- -----------------------------------------------------------------------------
-- 1) ADD OPERATION_ID
-- -----------------------------------------------------------------------------
SET @col_op := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'OPERATION_ID'
);

SET @sql := IF(
  @col_op = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `OPERATION_ID` varchar(64) DEFAULT NULL
       COMMENT ''Idempotency — retry cùng OperationId → Noop''
       AFTER `ACTION_CODE`',
  'SELECT ''OPERATION_ID already exists'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 2) ADD STABLE_KEY
-- -----------------------------------------------------------------------------
SET @col_sk := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'STABLE_KEY'
);

SET @sql := IF(
  @col_sk = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `STABLE_KEY` varchar(160) DEFAULT NULL
       COMMENT ''PC_HIST:{COMPANY}:{RUN}:v{VER}:{STEP}:{ACTION}:op:{OperationId}''
       AFTER `OPERATION_ID`',
  'SELECT ''STABLE_KEY already exists'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 3) ADD STEP_CODE
-- -----------------------------------------------------------------------------
SET @col_sc := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'STEP_CODE'
);

SET @sql := IF(
  @col_sc = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `STEP_CODE` varchar(40) DEFAULT NULL
       COMMENT ''STEP_CODE G-K (COST_ALLOCATION, …)''
       AFTER `STEP_INDEX`',
  'SELECT ''STEP_CODE already exists'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 4) UNIQUE (COMPANY_CD, STABLE_KEY) — NULL legacy được phép nhiều dòng (MySQL UNIQUE)
-- -----------------------------------------------------------------------------
SET @uk_stable := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'uk_pc_hist_stable'
);

-- Precheck trùng STABLE_KEY không-NULL trước khi ADD UNIQUE
SET @dup_stable := (
  SELECT COUNT(*) FROM (
    SELECT 1
    FROM production_cost_history
    WHERE STABLE_KEY IS NOT NULL AND TRIM(STABLE_KEY) <> ''
    GROUP BY COMPANY_CD, STABLE_KEY
    HAVING COUNT(*) > 1
    LIMIT 1
  ) x
);

SET @stop_dup := IF(
  IFNULL(@dup_stable, 0) = 0,
  'SELECT ''history STABLE_KEY precheck PASS'' AS NOTE',
  'GK2C06A_HISTORY_DUP_STABLE_KEY__FIX_DATA_BEFORE_UNIQUE'
);
PREPARE stmt_dup FROM @stop_dup;
EXECUTE stmt_dup;
DEALLOCATE PREPARE stmt_dup;

SET @sql := IF(
  @uk_stable > 0,
  'SELECT ''uk_pc_hist_stable already exists'' AS NOTE',
  'ALTER TABLE `production_cost_history`
     ADD UNIQUE KEY `uk_pc_hist_stable` (`COMPANY_CD`, `STABLE_KEY`)'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 5) INDEX lookup OperationId
-- -----------------------------------------------------------------------------
SET @idx_op := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'idx_pc_hist_operation'
);

SET @sql := IF(
  @idx_op > 0,
  'SELECT ''idx_pc_hist_operation already exists'' AS NOTE',
  'ALTER TABLE `production_cost_history`
     ADD KEY `idx_pc_hist_operation` (`COMPANY_CD`, `OPERATION_ID`, `RUN_ID`)'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT '2C-06A DDL history OPERATION_ID/STABLE_KEY/STEP_CODE applied (idempotent)' AS NOTE;
