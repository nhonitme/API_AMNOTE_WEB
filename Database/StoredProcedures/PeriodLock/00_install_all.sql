-- Install Period Lock stored procedures (steps 1–3)
-- Target: company database (e.g. am_web_001)
-- MySQL 5.6+
--
-- Prerequisites:
--   - acc_period_lock_step, acc_period_lock_month metadata tables
--   - sp_period_lock_step_upsert and related metadata SPs
--   - chitinfo (+ ext, description), chitdetailinfo (+ ext, description)
--   - chitinfo.INPUT_TYPE = 'LOCK', LOCK_STEP_CODE per step
--   - closingmonth_detail (CLOSE_YMD char(8), ngày cuối tháng khóa sổ)
--   - FA tables for step 1 (fa_asset_depre_posted, ...)
--
-- Run files in order:
--   FaPrepaid/01_helpers.sql
--   FaPrepaid/02–05 (validate, calculate, create_voucher, unlock)
--   Common/01_helpers.sql
--   CogsSummary/01_sp_period_lock_cogs.sql
--   ProfitLoss/01_sp_period_lock_pl.sql

SOURCE FaPrepaid/01_helpers.sql;
SOURCE FaPrepaid/02_sp_period_lock_fa_prepaid_validate.sql;
SOURCE FaPrepaid/03_sp_period_lock_fa_prepaid_calculate.sql;
SOURCE FaPrepaid/04_sp_period_lock_fa_prepaid_create_voucher.sql;
SOURCE FaPrepaid/05_sp_period_lock_fa_prepaid_unlock.sql;
SOURCE Common/01_helpers.sql;
SOURCE Common/02_transfer_helpers.sql;
SOURCE CogsSummary/01_sp_period_lock_cogs.sql;
SOURCE ProfitLoss/01_sp_period_lock_pl.sql;
