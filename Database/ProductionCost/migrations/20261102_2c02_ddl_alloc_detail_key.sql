-- =============================================================================
-- G-K 2C-02 — DDL điều chỉnh UNIQUE production_cost_alloc_detail
-- BÀN GIAO — user tự SOURCE trên DEV/TEST khi schema CŨ thiếu cột/UNIQUE.
-- Đồng bộ khóa C#: (PRODUCT_ID, DEPARTMENT_ID, ACCOUNT_CD, POOL_DEPARTMENT_ID)
-- CẤM production.
--
-- ĐIỀU KIỆN SỬ DỤNG (tránh chạy không cần thiết):
--   CHẠY khi bảng đã tạo từ bản 2B cũ (UNIQUE chỉ RUN_ID+VERSION+PRODUCT+ACCOUNT,
--   hoặc thiếu POOL_DEPARTMENT_ID).
--   KHÔNG CẦN khi cài mới từ 01_production_cost_tables.sql / 2b_01_tables.sql
--   bản repo hiện tại (đã có POOL_DEPARTMENT_ID + UNIQUE đủ khóa).
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

-- Thêm POOL_DEPARTMENT_ID (0 = pool dùng chung)
SET @col_exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_alloc_detail'
    AND COLUMN_NAME = 'POOL_DEPARTMENT_ID'
);

SET @sql := IF(
  @col_exists = 0,
  'ALTER TABLE `production_cost_alloc_detail`
     ADD COLUMN `POOL_DEPARTMENT_ID` bigint(20) NOT NULL DEFAULT 0
       COMMENT ''DEPARTMENT_ID của dòng pool nguồn; 0 = dùng chung''
       AFTER `DEPARTMENT_ID`',
  'SELECT ''POOL_DEPARTMENT_ID already exists'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Đổi UNIQUE: không mất dòng shared + dept pool / cùng PRODUCT nhiều DEPT
SET @uk_exists := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'production_cost_alloc_detail'
    AND INDEX_NAME = 'uk_pc_alloc_detail'
);

SET @sql := IF(
  @uk_exists > 0,
  'ALTER TABLE `production_cost_alloc_detail` DROP INDEX `uk_pc_alloc_detail`',
  'SELECT ''uk_pc_alloc_detail absent'' AS NOTE'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

ALTER TABLE `production_cost_alloc_detail`
  ADD UNIQUE KEY `uk_pc_alloc_detail`
    (`RUN_ID`, `RUN_VERSION`, `PRODUCT_ID`, `DEPARTMENT_ID`, `ACCOUNT_CD`, `POOL_DEPARTMENT_ID`);

SELECT '2C-02 DDL alloc_detail key applied' AS NOTE;
