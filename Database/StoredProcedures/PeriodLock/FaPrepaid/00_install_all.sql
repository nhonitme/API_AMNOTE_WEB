-- Install FA prepaid period lock stored procedures
-- Target: company database (e.g. am_web_001)
-- MySQL 5.6+
--
-- Prerequisites:
--   - Tables: fa_asset, fa_asset_depre_alloc, fa_asset_depre_posted, fa_asset_depre_posted_alloc
--   - chitinfo (+ chitdescriptioninfo), chitdetailinfo (+ chitdetaildescriptioninfo)
--   - chitinfo.INPUT_TYPE supports value 'LOCK', LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
--   - sys_code_sequence configured for OBJECT_TYPE = 'OT'
--
-- Run files in order:
--   01_helpers.sql
--   02_sp_period_lock_fa_prepaid_validate.sql
--   03_sp_period_lock_fa_prepaid_calculate.sql
--   04_sp_period_lock_fa_prepaid_create_voucher.sql
--   05_sp_period_lock_fa_prepaid_unlock.sql

SOURCE 01_helpers.sql;
SOURCE 02_sp_period_lock_fa_prepaid_validate.sql;
SOURCE 03_sp_period_lock_fa_prepaid_calculate.sql;
SOURCE 04_sp_period_lock_fa_prepaid_create_voucher.sql;
SOURCE 05_sp_period_lock_fa_prepaid_unlock.sql;
