-- =============================================================================
-- G-K 2B INSTALL — DEV/TEST ONLY
-- =============================================================================
-- Trước khi chạy:
--   1) Xác nhận rõ: SELECT DATABASE(); phải là DB DEV/TEST (không phải production).
--   2) Backup full schema + data DEV/TEST.
--   3) USE <ten_db_dev>;
-- CẤM chạy trên production. Không sửa Database/database.sql như migration.
-- =============================================================================

SELECT DATABASE() AS current_database,
       NOW() AS started_at,
       'CONFIRM this is DEV/TEST before continuing' AS safety_check;

-- 1) DDL
SOURCE 20261102_2b_01_tables.sql;

-- 2) Exclusive COST_ENGINE
SOURCE 20261102_2b_02_sp_engine.sql;

-- 3) NVL basis (anti double-count)
SOURCE 20261102_2b_03_sp_nvl_basis.sql;

-- 4) PeriodLock COGS + EXP_TO_154_ONLY
SOURCE 20261102_2b_04_sp_period_lock_cogs.sql;

-- 5) Verify (read-only)
SOURCE 20261102_2b_99_verify_readonly.sql;

SELECT 'G-K 2B install scripts finished — review verify output' AS status, NOW() AS finished_at;
