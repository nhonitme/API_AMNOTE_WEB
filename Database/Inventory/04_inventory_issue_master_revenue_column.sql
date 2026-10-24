-- AMNote Web - inventory issue (IO) header/list grid
-- Run against the MANAGER database (sys_grid_column), MySQL 5.6+.
-- Adds REVENUE_AMOUNT to the Info/master grid catalog only.
-- Safe to run repeatedly: does not overwrite existing user/system layout.
INSERT INTO sys_grid_column (
    GRID_ID, FIELD_NAME, LABEL_TEXT, CAPTION,
    IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH,
    IS_FIXED, FIXED_POSITION, ALLOW_HIDING,
    ALIGN, FORMAT_TYPE, SORT_ORDER, SORT_INDEX,
    CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT, ISDEL
)
SELECT
    'inventory-issue-master-grid', 'REVENUE_AMOUNT', 'REVENUE_AMOUNT', 'Tổng doanh thu',
    '1',
    COALESCE((
        SELECT MAX(s.VISIBLE_INDEX)
        FROM sys_grid_column s
        WHERE s.GRID_ID = 'inventory-issue-master-grid'
          AND COALESCE(s.ISDEL, '0') = '0'
    ), 0) + 1,
    160,
    '0', NULL, '1',
    'right',
    COALESCE((
        SELECT s.FORMAT_TYPE
        FROM sys_grid_column s
        WHERE s.GRID_ID = 'inventory-issue-master-grid'
          AND s.FIELD_NAME = 'AMOUNT'
          AND COALESCE(s.ISDEL, '0') = '0'
        LIMIT 1
    ), 'N2'),
    NULL, NULL,
    'SYSTEM', NOW(), 'SYSTEM', NOW(), '0'
WHERE NOT EXISTS (
    SELECT 1
    FROM sys_grid_column existing
    WHERE existing.GRID_ID = 'inventory-issue-master-grid'
      AND existing.FIELD_NAME = 'REVENUE_AMOUNT'
      AND COALESCE(existing.ISDEL, '0') = '0'
);

-- Verify Info and detail column catalogs (neither is controlled by IS_COGS).
SELECT GRID_ID, FIELD_NAME, IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH, FORMAT_TYPE
FROM sys_grid_column
WHERE GRID_ID IN ('inventory-issue-master-grid', 'inventory-issue-detail-grid')
  AND COALESCE(ISDEL, '0') = '0'
ORDER BY GRID_ID, VISIBLE_INDEX, FIELD_NAME;
