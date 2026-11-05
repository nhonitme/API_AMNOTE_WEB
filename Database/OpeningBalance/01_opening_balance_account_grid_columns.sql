-- AMNote Web - số dư đầu kỳ tài khoản
-- Run against the MANAGER database (sys_grid_column), MySQL 5.6+.
-- Grid: opening-balance-account-grid
-- Safe to run repeatedly: does not overwrite an existing GRID_ID + FIELD_NAME row.

INSERT INTO sys_grid_column (
    GRID_ID, FIELD_NAME, LABEL_TEXT, CAPTION,
    IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH,
    IS_FIXED, FIXED_POSITION, ALLOW_HIDING,
    ALIGN, FORMAT_TYPE, SORT_ORDER, SORT_INDEX,
    CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT, ISDEL
)
SELECT
    cfg.GRID_ID, cfg.FIELD_NAME, cfg.LABEL_TEXT, cfg.CAPTION,
    cfg.IS_VISIBLE, cfg.VISIBLE_INDEX, cfg.COLUMN_WIDTH,
    cfg.IS_FIXED, cfg.FIXED_POSITION, cfg.ALLOW_HIDING,
    cfg.ALIGN, cfg.FORMAT_TYPE, NULL, NULL,
    'SYSTEM', NOW(), 'SYSTEM', NOW(), '0'
FROM (
    SELECT 'opening-balance-account-grid' AS GRID_ID, 'ROW_ID' AS FIELD_NAME, 'ROW_ID' AS LABEL_TEXT, 'ID dòng' AS CAPTION, '0' AS IS_VISIBLE, 0 AS VISIBLE_INDEX, 80 AS COLUMN_WIDTH, '0' AS IS_FIXED, NULL AS FIXED_POSITION, '0' AS ALLOW_HIDING, 'left' AS ALIGN, NULL AS FORMAT_TYPE
    UNION ALL
    SELECT 'opening-balance-account-grid', 'ACC_ID', 'ACC_ID', 'ID tài khoản', '0', 1, 80, '0', NULL, '0', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'ACC_CD', 'ACC_CD', 'Mã tài khoản', '1', 2, 130, '0', NULL, '1', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'ACC_NM_VIET', 'ACCTITLE_NM', 'Tên tài khoản', '1', 3, 220, '0', NULL, '1', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'ACC_NM_ENG', 'ACCTITLE_NM_ENG', 'Tên tài khoản (ENG)', '1', 4, 220, '0', NULL, '1', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'ACC_NM_KOR', 'ACCTITLE_NM_KOR', 'Tên tài khoản (KOR)', '1', 5, 220, '0', NULL, '1', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'ACC_NM_CHINA', 'ACCTITLE_NM_CHINA', 'Tên tài khoản (CHN)', '1', 6, 220, '0', NULL, '1', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'FC_TYPE', 'FC_TYPE', 'Loại tiền', '1', 7, 130, '0', NULL, '1', 'left', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'DEBIT', 'DEBIT', 'Nợ', '1', 8, 140, '0', NULL, '1', 'right', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'CREDIT', 'CREDIT', 'Có', '1', 9, 140, '0', NULL, '1', 'right', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'DEBIT_FC', 'DEBIT_FC', 'Nợ ngoại tệ', '1', 10, 140, '0', NULL, '1', 'right', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'CREDIT_FC', 'CREDIT_FC', 'Có ngoại tệ', '1', 11, 140, '0', NULL, '1', 'right', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'EXCHANGE_RATE', 'FC_RATE', 'Tỷ giá', '1', 12, 130, '0', NULL, '1', 'right', NULL
    UNION ALL
    SELECT 'opening-balance-account-grid', 'NOTE', 'NOTE', 'Ghi chú', '1', 13, 180, '0', NULL, '1', 'left', NULL
) AS cfg
WHERE NOT EXISTS (
    SELECT 1
    FROM sys_grid_column existing
    WHERE existing.GRID_ID = cfg.GRID_ID
      AND existing.FIELD_NAME = cfg.FIELD_NAME
      AND COALESCE(existing.ISDEL, '0') = '0'
);

SELECT GRID_ID, FIELD_NAME, LABEL_TEXT, CAPTION, IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH
FROM sys_grid_column
WHERE GRID_ID = 'opening-balance-account-grid'
  AND COALESCE(ISDEL, '0') = '0'
ORDER BY VISIBLE_INDEX, FIELD_NAME;
