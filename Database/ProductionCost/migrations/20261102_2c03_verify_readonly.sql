-- G-K 2C-03 — verify SELECT only
-- 0 DML / 0 DDL

SELECT '2C03 wip' AS kind, ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_pc_source_rebuild', 'sp_pc_wip_calculate', 'sp_pc_cost_confirm'
  );

SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('production_cost_wip', 'production_cost_source', 'production_cost_receipt');
