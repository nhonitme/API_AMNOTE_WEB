-- =============================================================================
-- G-K 2B ROLLBACK — DEV/TEST ONLY
-- =============================================================================
-- ƯU TIÊN: restore mysqldump backup DEV/TEST thay vì script này khi cần khôi phục toàn diện.
-- Hard-stop: SOURCE preflight TRƯỚC mọi DROP (0 DDL trong preflight).
-- Chỉ DROP bảng/SP thuộc inventory 2B (tồn tại trên schema hiện tại).
-- CẤM production.
-- =============================================================================

SOURCE 20261102_2b_00_preflight_dev.sql;

SELECT DATABASE() AS current_database,
       @@hostname AS current_host,
       'ROLLBACK DEV — preflight PASS; prefer backup restore for full recovery' AS safety_check,
       NOW() AS started_at;

-- Inventory: liệt kê object 2B trước khi xóa (chỉ đọc)
SELECT 'BEFORE_DROP_TABLE' AS phase, TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'production_cost_%'
ORDER BY TABLE_NAME;

SELECT 'BEFORE_DROP_ROUTINE' AS phase, ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND (
        ROUTINE_NAME LIKE 'sp_pc_engine_%'
     OR ROUTINE_NAME LIKE 'sp_pc_nvl_%'
     OR ROUTINE_NAME = 'sp_period_lock_cogs_assert_can_overwrite'
     OR ROUTINE_NAME = 'sp_pc_2b_require_allowed_db'
  )
ORDER BY ROUTINE_NAME;

-- Drop SP 2B chỉ khi tồn tại
DROP PROCEDURE IF EXISTS `sp_pc_2b_require_allowed_db`;
DROP PROCEDURE IF EXISTS `sp_pc_engine_check`;
DROP PROCEDURE IF EXISTS `sp_pc_engine_claim`;
DROP PROCEDURE IF EXISTS `sp_pc_engine_claim_for_cogs`;
DROP PROCEDURE IF EXISTS `sp_pc_engine_assert_for_cogs`;
DROP PROCEDURE IF EXISTS `sp_pc_engine_release_cogs`;
DROP PROCEDURE IF EXISTS `sp_pc_nvl_basis_build`;
DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_assert_can_overwrite`;

-- Khôi phục PeriodLock COGS B-I (file đi kèm — cũng có guard)
SOURCE 20261102_2b_00_guard_inline.sql;
SOURCE 20261102_2b_rollback_cogs_bi_only.sql;

-- DROP bảng production_cost_* chỉ nếu còn trên schema (IF EXISTS)
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS `production_cost_history`;
DROP TABLE IF EXISTS `production_cost_voucher_ref`;
DROP TABLE IF EXISTS `production_cost_receipt`;
DROP TABLE IF EXISTS `production_cost_wip`;
DROP TABLE IF EXISTS `production_cost_source`;
DROP TABLE IF EXISTS `production_cost_nvl_orphan`;
DROP TABLE IF EXISTS `production_cost_nvl_basis_line`;
DROP TABLE IF EXISTS `production_cost_nvl_basis`;
DROP TABLE IF EXISTS `production_cost_alloc_detail`;
DROP TABLE IF EXISTS `production_cost_alloc_pool`;
DROP TABLE IF EXISTS `production_cost_object`;
DROP TABLE IF EXISTS `production_cost_step`;
DROP TABLE IF EXISTS `production_cost_run`;
DROP TABLE IF EXISTS `production_cost_period`;
SET FOREIGN_KEY_CHECKS = 1;

SELECT 'AFTER_DROP_TABLE' AS phase, TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'production_cost_%'
ORDER BY TABLE_NAME;

SELECT 'G-K 2B rollback finished (prefer verify against backup)' AS status, NOW() AS finished_at;
