-- =============================================================================
-- G-K 2C-06A — DDL: history idempotency (MySQL 5.6/8.0 an toàn, không innodb_large_prefix)
-- BÀN GIAO — user tự SOURCE trên DEV/TEST. Agent không tự chạy.
-- CẤM production. Không bật EnableWrites trong bước SOURCE này.
--
-- Index utf8 (3 byte/char):
--   UNIQUE (COMPANY_CD VARCHAR(20) + STABLE_KEY VARCHAR(190)) = 630 bytes ≤ 767
--   UNIQUE (COMPANY_CD + OPERATION_ID VARCHAR(64)) = 252 bytes ≤ 767
-- Không dùng prefix unique (tránh hai khóa khác nhau bị coi trùng).
-- Canonical STABLE_KEY dài → C# hash PC_HIST_H:{sha256} (73 ký tự).
-- CONTENT_FINGERPRINT VARCHAR(64) — đọc/ghi thật, không SELECT NULL.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

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
-- 1) OPERATION_ID VARCHAR(64)
-- -----------------------------------------------------------------------------
SET @col_op := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'OPERATION_ID'
);
SET @sql := IF(
  @col_op = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `OPERATION_ID` varchar(64) DEFAULT NULL
       COMMENT ''Idempotency — UNIQUE(COMPANY_CD,OPERATION_ID)''
       AFTER `ACTION_CODE`',
  'SELECT ''OPERATION_ID already exists'' AS NOTE'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 2) STABLE_KEY VARCHAR(190) — an toàn MySQL 5.6 utf8 unique
--    Drop UNIQUE cũ trước khi MODIFY nếu đang > 190
-- -----------------------------------------------------------------------------
SET @col_sk := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'STABLE_KEY'
);
SET @sk_len := (
  SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'STABLE_KEY' LIMIT 1
);
SET @uk_stable := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'uk_pc_hist_stable'
);

-- Nếu STABLE_KEY quá dài cho unique 5.6: drop unique trước khi thu hẹp
SET @sql := IF(
  @uk_stable > 0 AND IFNULL(@sk_len, 0) > 190,
  'ALTER TABLE `production_cost_history` DROP INDEX `uk_pc_hist_stable`',
  'SELECT ''uk_pc_hist_stable keep/skip before resize'' AS NOTE'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @uk_stable := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'uk_pc_hist_stable'
);

SET @sql := IF(
  @col_sk = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `STABLE_KEY` varchar(190) DEFAULT NULL
       COMMENT ''PC_HIST:… hoặc PC_HIST_H:{sha256} — max 190 utf8; UNIQUE full column''
       AFTER `OPERATION_ID`',
  IF(
    IFNULL(@sk_len, 0) <> 190,
    'ALTER TABLE `production_cost_history`
       MODIFY COLUMN `STABLE_KEY` varchar(190) DEFAULT NULL
         COMMENT ''PC_HIST:… hoặc PC_HIST_H:{sha256} — max 190 utf8; UNIQUE full column''',
    'SELECT ''STABLE_KEY already varchar(190)'' AS NOTE'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 3) STEP_CODE
-- -----------------------------------------------------------------------------
SET @col_sc := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'STEP_CODE'
);
SET @sql := IF(
  @col_sc = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `STEP_CODE` varchar(40) DEFAULT NULL
       COMMENT ''STEP_CODE G-K''
       AFTER `STEP_INDEX`',
  'SELECT ''STEP_CODE already exists'' AS NOTE'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 4) CONTENT_FINGERPRINT VARCHAR(64) — nguồn tin cậy fingerprint (không SELECT NULL)
-- -----------------------------------------------------------------------------
SET @col_fp := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND COLUMN_NAME = 'CONTENT_FINGERPRINT'
);
SET @sql := IF(
  @col_fp = 0,
  'ALTER TABLE `production_cost_history`
     ADD COLUMN `CONTENT_FINGERPRINT` varchar(64) DEFAULT NULL
       COMMENT ''Hash nội dung nghiệp vụ — Confirm Noop/Conflict''
       AFTER `STABLE_KEY`',
  'SELECT ''CONTENT_FINGERPRINT already exists'' AS NOTE'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 5) UNIQUE (COMPANY_CD, STABLE_KEY) — full column, không prefix
-- -----------------------------------------------------------------------------
SET @uk_stable := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'uk_pc_hist_stable'
);
SET @dup_stable := (
  SELECT COUNT(*) FROM (
    SELECT 1 FROM production_cost_history
    WHERE STABLE_KEY IS NOT NULL AND TRIM(STABLE_KEY) <> ''
    GROUP BY COMPANY_CD, STABLE_KEY HAVING COUNT(*) > 1 LIMIT 1
  ) x
);
SET @stop_dup := IF(
  IFNULL(@dup_stable, 0) = 0,
  'SELECT ''history STABLE_KEY precheck PASS'' AS NOTE',
  'GK2C06A_HISTORY_DUP_STABLE_KEY__FIX_DATA_BEFORE_UNIQUE'
);
PREPARE stmt_dup FROM @stop_dup; EXECUTE stmt_dup; DEALLOCATE PREPARE stmt_dup;

SET @sql := IF(
  @uk_stable > 0,
  'SELECT ''uk_pc_hist_stable already exists'' AS NOTE',
  'ALTER TABLE `production_cost_history`
     ADD UNIQUE KEY `uk_pc_hist_stable` (`COMPANY_CD`, `STABLE_KEY`)'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- 6) UNIQUE (COMPANY_CD, OPERATION_ID)
-- -----------------------------------------------------------------------------
SET @idx_op_old := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'idx_pc_hist_operation'
);
SET @sql := IF(
  @idx_op_old > 0,
  'ALTER TABLE `production_cost_history` DROP INDEX `idx_pc_hist_operation`',
  'SELECT ''idx_pc_hist_operation absent'' AS NOTE'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @uk_op := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_history'
    AND INDEX_NAME = 'uk_pc_hist_operation'
);
SET @dup_op := (
  SELECT COUNT(*) FROM (
    SELECT 1 FROM production_cost_history
    WHERE OPERATION_ID IS NOT NULL AND TRIM(OPERATION_ID) <> ''
    GROUP BY COMPANY_CD, OPERATION_ID HAVING COUNT(*) > 1 LIMIT 1
  ) x
);
SET @stop_dup_op := IF(
  IFNULL(@dup_op, 0) = 0,
  'SELECT ''history OPERATION_ID precheck PASS'' AS NOTE',
  'GK2C06A_HISTORY_DUP_OPERATION_ID__FIX_DATA_BEFORE_UNIQUE'
);
PREPARE stmt_dup_op FROM @stop_dup_op; EXECUTE stmt_dup_op; DEALLOCATE PREPARE stmt_dup_op;

SET @sql := IF(
  @uk_op > 0,
  'SELECT ''uk_pc_hist_operation already exists'' AS NOTE',
  'ALTER TABLE `production_cost_history`
     ADD UNIQUE KEY `uk_pc_hist_operation` (`COMPANY_CD`, `OPERATION_ID`)'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT '2C-06A DDL history VARCHAR(190)+CONTENT_FINGERPRINT+UNIQUE applied' AS NOTE;
