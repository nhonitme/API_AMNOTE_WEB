-- =============================================================================
-- G-K 2B ROLLBACK — DEV/TEST ONLY
-- =============================================================================
-- 1) USE đúng DB DEV đã backup.
-- 2) Khôi phục COGS SP về hành vi B-I 3 rule (file đi kèm).
-- 3) DROP SP engine/nvl + bảng production_cost_* (chỉ DEV — mất dữ liệu G-K).
-- Prefer: restore từ backup mysqldump thay vì script này nếu đã có dữ liệu thật.
-- CẤM production.
-- =============================================================================

SELECT DATABASE() AS current_database,
       'ROLLBACK DEV — confirm backup exists' AS safety_check,
       NOW() AS started_at;

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_pc_engine_claim`$$
DROP PROCEDURE IF EXISTS `sp_pc_engine_assert_for_cogs`$$
DROP PROCEDURE IF EXISTS `sp_pc_engine_release_cogs`$$
DROP PROCEDURE IF EXISTS `sp_pc_nvl_basis_build`$$

DELIMITER ;

-- Khôi phục PeriodLock COGS (3 rule B-I, không EXP_TO_154_ONLY / không engine assert)
SOURCE 20261102_2b_rollback_cogs_bi_only.sql;

-- DROP bảng G-K (thứ tự phụ thuộc mềm — không FK cứng)
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

SELECT 'G-K 2B rollback finished' AS status, NOW() AS finished_at;
