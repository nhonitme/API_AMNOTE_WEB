-- G-K 2C-02 — verify SELECT only (user tự chạy trên DEV sau khi SOURCE script alloc)
-- 0 DML / 0 DDL

SELECT '2B engine' AS kind, ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_pc_engine_check', 'sp_pc_engine_claim', 'sp_pc_nvl_basis_build',
    'sp_period_lock_cogs_create_voucher'
  );

SELECT '2C02 alloc' AS kind, ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_pc_alloc_load_pool', 'sp_pc_alloc_load_basis_qty',
    'sp_pc_alloc_calculate', 'sp_pc_alloc_confirm'
  );

-- COGS SP còn nhận EXP_TO_154_ONLY và 3 rule B-I
SELECT ROUTINE_NAME,
       CASE WHEN ROUTINE_DEFINITION LIKE '%EXP_TO_154_ONLY%' THEN 'YES' ELSE 'NO' END AS has_exp_only,
       CASE WHEN ROUTINE_DEFINITION LIKE '%154_TO_155%' THEN 'YES' ELSE 'NO' END AS has_bi_rules
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME LIKE '%cogs%create_voucher%';
