-- Drop legacy / unused Period Lock procedures on company DB (optional cleanup)
-- Run after deploying Common + Cogs + PL stores from repo.
-- MySQL 5.6+

DROP PROCEDURE IF EXISTS `sp_period_lock_run_fa_prepaid`;
DROP PROCEDURE IF EXISTS `sp_period_lock_run_cogs_transfer`;
DROP PROCEDURE IF EXISTS `sp_period_lock_run_profit_loss`;

-- Not called by PeriodLockRepository (verify no other app module uses them first)
DROP PROCEDURE IF EXISTS `sp_period_lock_get_actual_from_period`;
DROP PROCEDURE IF EXISTS `sp_period_lock_get_actual_unlock_to_period`;
DROP PROCEDURE IF EXISTS `sp_period_lock_latest_unlockable_period_get`;

-- Removed from repo (replaced by inline PS queries in build_xfer)
DROP PROCEDURE IF EXISTS `sp_period_lock_get_period_debit`;
DROP PROCEDURE IF EXISTS `sp_period_lock_get_period_credit`;
DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_validate_xfer`;
DROP PROCEDURE IF EXISTS `sp_period_lock_pl_validate_xfer`;
