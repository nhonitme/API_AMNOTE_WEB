-- =============================================================================
-- G-K 2B INSTALL — DEV/TEST ONLY
-- =============================================================================
-- Preflight bắt buộc (hard-stop nếu sai DB):
--   SET @GK_2B_ALLOW_DB = '<ten_db_dev_da_duyet>';
--   USE <ten_db_dev_da_duyet>;
--   SOURCE 20261102_2b_00_preflight_dev.sql;
--   SOURCE 20261102_2b_00_install_dev.sql;
-- CẤM production. Không sửa Database/database.sql như migration.
-- =============================================================================

CALL sp_pc_2b_require_allowed_db();

SELECT DATABASE() AS current_database,
       @GK_2B_ALLOW_DB AS allowed_database,
       NOW() AS started_at;

SOURCE 20261102_2b_01_tables.sql;
SOURCE 20261102_2b_02_sp_engine.sql;
SOURCE 20261102_2b_03_sp_nvl_basis.sql;
SOURCE 20261102_2b_04_sp_period_lock_cogs.sql;
SOURCE 20261102_2b_99_verify_readonly.sql;

SELECT 'G-K 2B install scripts finished — review verify output' AS status, NOW() AS finished_at;
