-- G-K 2C-02 — verify SELECT only (user tự chạy trên DEV sau khi SOURCE script alloc)
-- 0 DML / 0 DDL

SELECT '2B engine' AS kind, ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_pc_engine_check', 'sp_pc_engine_claim', 'sp_pc_nvl_basis_build',
    'sp_period_lock_cogs_create_voucher', 'sp_period_lock_cogs_build_xfer'
  );

SELECT '2C02 alloc' AS kind, ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_pc_alloc_load_pool', 'sp_pc_alloc_load_basis_qty',
    'sp_pc_alloc_calculate', 'sp_pc_alloc_confirm',
    'sp_pc_assert_allocation_ready_for_exp'
  );

-- Confirm → ALLOC_CONFIRMED; load_pool bảo vệ; không TX nội bộ trong body
SELECT ROUTINE_NAME,
       CASE WHEN ROUTINE_DEFINITION LIKE '%ALLOC_CONFIRMED%' THEN 'YES' ELSE 'NO' END AS has_alloc_confirmed,
       CASE WHEN ROUTINE_DEFINITION LIKE '%START TRANSACTION%' THEN 'YES' ELSE 'NO' END AS has_start_tx
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN ('sp_pc_alloc_confirm', 'sp_pc_alloc_load_pool', 'sp_pc_assert_allocation_ready_for_exp');

-- PeriodLock EXP nhận assert / ALLOC_CONFIRMED; B-I còn
SELECT ROUTINE_NAME,
       CASE WHEN ROUTINE_DEFINITION LIKE '%EXP_TO_154_ONLY%' THEN 'YES' ELSE 'NO' END AS has_exp_only,
       CASE WHEN ROUTINE_DEFINITION LIKE '%sp_pc_assert_allocation_ready_for_exp%' THEN 'YES' ELSE 'NO' END AS calls_assert,
       CASE WHEN ROUTINE_DEFINITION LIKE '%154_TO_155%' THEN 'YES' ELSE 'NO' END AS has_bi_rules
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME LIKE '%period_lock_cogs%';

-- DDL khóa phân bổ
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_alloc_detail'
  AND COLUMN_NAME = 'POOL_DEPARTMENT_ID';

SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_alloc_detail'
  AND INDEX_NAME = 'uk_pc_alloc_detail'
GROUP BY INDEX_NAME;
