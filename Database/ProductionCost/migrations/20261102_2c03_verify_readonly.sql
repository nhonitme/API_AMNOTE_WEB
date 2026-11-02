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

SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_wip'
  AND INDEX_NAME = 'uk_pc_wip'
GROUP BY INDEX_NAME;

SELECT COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_receipt'
  AND COLUMN_NAME = 'DEPARTMENT_ID';
