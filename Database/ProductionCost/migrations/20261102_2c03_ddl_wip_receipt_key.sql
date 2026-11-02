-- =============================================================================
-- G-K 2C-03 — DDL nâng cấp UNIQUE WIP + DEPARTMENT_ID trên receipt
-- BÀN GIAO — user tự SOURCE trên DEV/TEST khi schema CŨ.
-- CẤM production. Agent không tự chạy.
--
-- ĐIỀU KIỆN:
--   CHẠY khi uk_pc_wip cũ thiếu DEPARTMENT_ID
--        hoặc production_cost_receipt thiếu DEPARTMENT_ID.
--   KHÔNG CẦN khi cài mới từ 01_production_cost_tables.sql / 2b_01 bản repo hiện tại.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

-- -----------------------------------------------------------------------------
-- 0a) Cấu trúc UNIQUE hiện tại
-- -----------------------------------------------------------------------------
SET @uk_cols := (
  SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_wip'
    AND INDEX_NAME = 'uk_pc_wip'
);

SET @uk_already_new := IF(
  IFNULL(@uk_cols, '') = 'RUN_ID,RUN_VERSION,PRODUCT_ID,DEPARTMENT_ID', 1, 0
);
SET @uk_is_old := IF(
  IFNULL(@uk_cols, '') = 'RUN_ID,RUN_VERSION,PRODUCT_ID', 1, 0
);

SELECT IFNULL(@uk_cols, '(absent)') AS current_uk_pc_wip_cols,
       @uk_already_new AS already_new,
       @uk_is_old AS is_old_product_only;

-- -----------------------------------------------------------------------------
-- 0b) Precheck trùng khóa MỚI — mọi dòng chịu UNIQUE (kể cả ISDEL <> '0')
-- -----------------------------------------------------------------------------
SELECT
    RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID, ISDEL, COUNT(*) AS CNT
FROM production_cost_wip
GROUP BY RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID, ISDEL
HAVING COUNT(*) > 1;

-- Trùng trên (RUN,VER,PRODUCT,DEPT) bất kể ISDEL — UNIQUE MySQL gồm soft-delete
SET @dup_any := (
  SELECT COUNT(*) FROM (
    SELECT 1
    FROM production_cost_wip
    GROUP BY RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID
    HAVING COUNT(*) > 1
    LIMIT 1
  ) x
);

-- Fail-stop: identifier không hợp lệ → ERROR khi còn trùng (mysql --abort-source-on-error)
SET @stop_dup := IF(
  IFNULL(@dup_any, 0) = 0,
  'SELECT ''WIP precheck PASS — no duplicate on new key (incl. ISDEL)'' AS NOTE',
  'GK2C03_WIP_DUP_PRODUCT_DEPT_INCL_ISDEL__FIX_DATA_BEFORE_ALTER'
);
PREPARE stmt_pre FROM @stop_dup;
EXECUTE stmt_pre;
DEALLOCATE PREPARE stmt_pre;

-- -----------------------------------------------------------------------------
-- 1) Đổi UNIQUE chỉ khi đang là khóa cũ (PRODUCT only)
-- -----------------------------------------------------------------------------
SET @sql := IF(
  @uk_already_new = 1,
  'SELECT ''uk_pc_wip already (RUN_ID,RUN_VERSION,PRODUCT_ID,DEPARTMENT_ID) — skip DROP/ADD'' AS NOTE',
  IF(
    @uk_is_old = 1,
    'ALTER TABLE `production_cost_wip` DROP INDEX `uk_pc_wip`',
    IF(
      IFNULL(@uk_cols, '') = '',
      'SELECT ''uk_pc_wip absent — will ADD'' AS NOTE',
      CONCAT('SELECT ''uk_pc_wip unexpected cols: ', IFNULL(@uk_cols, ''), ' — review manually'' AS NOTE')
    )
  )
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql := IF(
  @uk_already_new = 1,
  'SELECT ''uk_pc_wip ADD skipped'' AS NOTE',
  IF(
    @uk_is_old = 1 OR IFNULL(@uk_cols, '') = '',
    'ALTER TABLE `production_cost_wip` ADD UNIQUE KEY `uk_pc_wip` (`RUN_ID`,`RUN_VERSION`,`PRODUCT_ID`,`DEPARTMENT_ID`)',
    'SELECT ''uk_pc_wip ADD skipped — unexpected existing index'' AS NOTE'
  )
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

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

-- Receipt UNIQUE phải vẫn là RUN_ID+RUN_VERSION+INPUT_ID
SET @rcpt_uk := (
  SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_receipt'
    AND INDEX_NAME = 'uk_pc_receipt'
);
SELECT IFNULL(@rcpt_uk, '(absent)') AS uk_pc_receipt_cols;
SET @stop_rcpt := IF(
  IFNULL(@rcpt_uk, '') IN ('', 'RUN_ID,RUN_VERSION,INPUT_ID'),
  'SELECT ''receipt UNIQUE OK'' AS NOTE',
  'GK2C03_RECEIPT_UNIQUE_UNEXPECTED__REVIEW'
);
PREPARE stmt_r FROM @stop_rcpt;
EXECUTE stmt_r;
DEALLOCATE PREPARE stmt_r;

SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_wip'
  AND INDEX_NAME = 'uk_pc_wip'
GROUP BY INDEX_NAME;

SELECT '2C-03 DDL wip/receipt key applied (idempotent)' AS NOTE;
