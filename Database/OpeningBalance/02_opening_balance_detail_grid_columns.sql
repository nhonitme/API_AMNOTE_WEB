-- AMNote Web - số dư đầu kỳ khách hàng, ngân hàng, đối tượng THCP
-- Run against the MANAGER database (sys_grid_column), MySQL 5.6+.
-- Grids:
--   opening-balance-customer-grid
--   opening-balance-bank-grid
--   opening-balance-cost-object-grid
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
    SELECT 'opening-balance-customer-grid' AS GRID_ID, 'ROW_ID' AS FIELD_NAME, 'ROW_ID' AS LABEL_TEXT, 'ID dòng' AS CAPTION, '0' AS IS_VISIBLE, 0 AS VISIBLE_INDEX, 80 AS COLUMN_WIDTH, '0' AS IS_FIXED, NULL AS FIXED_POSITION, '0' AS ALLOW_HIDING, 'left' AS ALIGN, NULL AS FORMAT_TYPE
    UNION ALL SELECT 'opening-balance-customer-grid', 'CUSTOMER_ID', 'CUSTOMER_ID', 'ID khách hàng', '0', 1, 80, '0', NULL, '0', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'ACC_CD', 'ACC_CD', 'Mã tài khoản', '1', 2, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'ACC_NM_VIET', 'ACCTITLE_NM', 'Tên tài khoản', '1', 3, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'ACC_NM_ENG', 'ACCTITLE_NM_ENG', 'Tên tài khoản (ENG)', '1', 4, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'ACC_NM_KOR', 'ACCTITLE_NM_KOR', 'Tên tài khoản (KOR)', '1', 5, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'ACC_NM_CHINA', 'ACCTITLE_NM_CHINA', 'Tên tài khoản (CHN)', '1', 6, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CUSTOMER_CD', 'CUSTOMER_CD', 'Mã khách hàng', '1', 7, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CUSTOMER_NM_VIET', 'CUSTOMER_NM', 'Tên khách hàng', '1', 8, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CUSTOMER_NM_ENG', 'CUSTOMER_NM_ENG', 'Tên khách hàng (ENG)', '1', 9, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CUSTOMER_NM_KOR', 'CUSTOMER_NM_KOR', 'Tên khách hàng (KOR)', '1', 10, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CUSTOMER_NM_CHINA', 'CUSTOMER_NM_CHINA', 'Tên khách hàng (CHN)', '1', 11, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'FC_TYPE', 'FC_TYPE', 'Loại tiền', '1', 12, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'DEBIT', 'DEBIT', 'Nợ', '1', 13, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CREDIT', 'CREDIT', 'Có', '1', 14, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'DEBIT_FC', 'DEBIT_FC', 'Nợ ngoại tệ', '1', 15, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'CREDIT_FC', 'CREDIT_FC', 'Có ngoại tệ', '1', 16, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'EXCHANGE_RATE', 'FC_RATE', 'Tỷ giá', '1', 17, 130, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-customer-grid', 'NOTE', 'NOTE', 'Ghi chú', '1', 18, 180, '0', NULL, '1', 'left', NULL

    UNION ALL SELECT 'opening-balance-bank-grid', 'ROW_ID', 'ROW_ID', 'ID dòng', '0', 0, 80, '0', NULL, '0', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'BANK_ID', 'BANK_ID', 'ID ngân hàng', '0', 1, 80, '0', NULL, '0', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'ACC_CD', 'ACC_CD', 'Mã tài khoản', '1', 2, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'ACC_NM_VIET', 'ACCTITLE_NM', 'Tên tài khoản', '1', 3, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'ACC_NM_ENG', 'ACCTITLE_NM_ENG', 'Tên tài khoản (ENG)', '1', 4, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'ACC_NM_KOR', 'ACCTITLE_NM_KOR', 'Tên tài khoản (KOR)', '1', 5, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'ACC_NM_CHINA', 'ACCTITLE_NM_CHINA', 'Tên tài khoản (CHN)', '1', 6, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'BANK_CD', 'BANK_CD', 'Mã ngân hàng', '1', 7, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'BANK_NM_VIET', 'BANK_NM', 'Tên ngân hàng', '1', 8, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'BANK_NM_ENG', 'BANK_NM_ENG', 'Tên ngân hàng (ENG)', '1', 9, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'BANK_NM_KOR', 'BANK_NM_KOR', 'Tên ngân hàng (KOR)', '1', 10, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'BANK_NM_CHINA', 'BANK_NM_CHINA', 'Tên ngân hàng (CHN)', '1', 11, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'FC_TYPE', 'FC_TYPE', 'Loại tiền', '1', 12, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'DEBIT', 'DEBIT', 'Nợ', '1', 13, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'CREDIT', 'CREDIT', 'Có', '1', 14, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'DEBIT_FC', 'DEBIT_FC', 'Nợ ngoại tệ', '1', 15, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'CREDIT_FC', 'CREDIT_FC', 'Có ngoại tệ', '1', 16, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'EXCHANGE_RATE', 'FC_RATE', 'Tỷ giá', '1', 17, 130, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-bank-grid', 'NOTE', 'NOTE', 'Ghi chú', '1', 18, 180, '0', NULL, '1', 'left', NULL

    UNION ALL SELECT 'opening-balance-cost-object-grid', 'ROW_ID', 'ROW_ID', 'ID dòng', '0', 0, 80, '0', NULL, '0', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEPARTMENT_ID', 'DEPARTMENT_ID', 'ID đối tượng THCP', '0', 1, 80, '0', NULL, '0', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'ACC_CD', 'ACC_CD', 'Mã tài khoản', '1', 2, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'ACC_NM_VIET', 'ACCTITLE_NM', 'Tên tài khoản', '1', 3, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'ACC_NM_ENG', 'ACCTITLE_NM_ENG', 'Tên tài khoản (ENG)', '1', 4, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'ACC_NM_KOR', 'ACCTITLE_NM_KOR', 'Tên tài khoản (KOR)', '1', 5, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'ACC_NM_CHINA', 'ACCTITLE_NM_CHINA', 'Tên tài khoản (CHN)', '1', 6, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEPARTMENT_CD', 'DEPARTMENT_CD', 'Mã đối tượng THCP', '1', 7, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEP_NM_VIET', 'DEP_NM', 'Tên đối tượng THCP', '1', 8, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEP_NM_ENG', 'DEP_NM_ENG', 'Tên đối tượng THCP (ENG)', '1', 9, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEP_NM_KOR', 'DEP_NM_KOR', 'Tên đối tượng THCP (KOR)', '1', 10, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEP_NM_CHINA', 'DEP_NM_CHINA', 'Tên đối tượng THCP (CHN)', '1', 11, 220, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'FC_TYPE', 'FC_TYPE', 'Loại tiền', '1', 12, 130, '0', NULL, '1', 'left', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEBIT', 'DEBIT', 'Nợ', '1', 13, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'CREDIT', 'CREDIT', 'Có', '1', 14, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'DEBIT_FC', 'DEBIT_FC', 'Nợ ngoại tệ', '1', 15, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'CREDIT_FC', 'CREDIT_FC', 'Có ngoại tệ', '1', 16, 140, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'EXCHANGE_RATE', 'FC_RATE', 'Tỷ giá', '1', 17, 130, '0', NULL, '1', 'right', NULL
    UNION ALL SELECT 'opening-balance-cost-object-grid', 'NOTE', 'NOTE', 'Ghi chú', '1', 18, 180, '0', NULL, '1', 'left', NULL
) AS cfg
WHERE NOT EXISTS (
    SELECT 1
    FROM sys_grid_column existing
    WHERE existing.GRID_ID = cfg.GRID_ID
      AND existing.FIELD_NAME = cfg.FIELD_NAME
      AND COALESCE(existing.ISDEL, '0') = '0'
);

SELECT GRID_ID, FIELD_NAME, IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH
FROM sys_grid_column
WHERE GRID_ID IN (
    'opening-balance-customer-grid',
    'opening-balance-bank-grid',
    'opening-balance-cost-object-grid'
)
  AND COALESCE(ISDEL, '0') = '0'
ORDER BY GRID_ID, VISIBLE_INDEX, FIELD_NAME;
