-- =============================================================================
-- G-K 2B INSTALL — DEV/TEST ONLY
-- =============================================================================
-- Bắt buộc chạy bằng mysql client với abort-on-error, từ thư mục migrations:
--   mysql --abort-source-on-error ... < 20261102_2b_00_install_dev.sql
-- hoặc: .\Invoke-Gk2bInstall.ps1
--
-- Thứ tự: preflight (0 DDL) → nếu FAIL dừng toàn bộ → mới DDL/SP.
-- CẤM production.
-- =============================================================================

SOURCE 20261102_2b_00_preflight_dev.sql;

-- Đến đây = preflight PASS. Tiếp tục migration.
SELECT DATABASE() AS current_database,
       @@hostname AS current_host,
       @GK_2B_APPROVED_DB AS approved_database,
       NOW() AS install_started_at;

SOURCE 20261102_2b_01_tables.sql;
SOURCE 20261102_2b_02_sp_engine.sql;
SOURCE 20261102_2b_03_sp_nvl_basis.sql;
SOURCE 20261102_2b_04_sp_period_lock_cogs.sql;
SOURCE 20261102_2b_99_verify_readonly.sql;

SELECT 'G-K 2B install finished — review verify output' AS status, NOW() AS finished_at;
