-- =============================================================================
-- G-K 2C-02 — Patch PeriodLock EXP_TO_154_ONLY: nhận ALLOC_CONFIRMED (B2)
-- Áp dụng KHI 2B_04 đã cài trước khi có thay đổi nguồn 2b_04.
-- Nếu cài mới từ source 2b_04 đã vá + 2c02_sp_alloc → không cần file này.
--
-- Không đổi 3 rule B-I.
-- Cần: sp_pc_assert_allocation_ready_for_exp (trong 20261102_2c02_sp_alloc.sql)
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

-- Ghi chú vận hành:
-- 1) SOURCE 20261102_2c02_sp_alloc.sql (có sp_pc_assert_allocation_ready_for_exp)
-- 2) Thay block STATUS=CONFIRMED trong sp_period_lock_cogs_build_xfer nhánh EXP_TO_154_ONLY
--    bằng: CALL sp_pc_assert_allocation_ready_for_exp(p_COMPANY_CD, p_PERIOD_YM);
-- 3) Hoặc cài lại 20261102_2b_04_sp_period_lock_cogs.sql bản đã vá trong repo.
--
-- Verify:
SELECT ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME = 'sp_pc_assert_allocation_ready_for_exp';

SELECT 'Apply assert SP then reinstall patched 2b_04 OR edit EXP branch manually' AS NOTE;
