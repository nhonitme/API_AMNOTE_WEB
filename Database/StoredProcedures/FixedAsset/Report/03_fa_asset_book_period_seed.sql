-- =============================================================================
-- FA_ASSET_BOOK_REPORT + FA_DEPRECIATION_PERIOD_REPORT
-- Seed catalog + mapping + PDF header (am_web_manager).
-- SP phải deploy trên company DB trước khi preview chạy được:
--   rpt_fa_asset_book
--   rpt_fa_depreciation_period
--
-- REPORT_ID gợi ý (đổi nếu DB đã dùng):
--   68 = FA_ASSET_BOOK_REPORT
--   69 = FA_DEPRECIATION_PERIOD_REPORT
-- Menu: xem 01_fa_report_menu_seed.sql
--
-- Lưu ý collation: bảng dùng utf8_unicode_ci — biến session phải cùng collation
-- để tránh Error 1267 (Illegal mix of collations).
-- =============================================================================

SET @v_company_cd = CONVERT('0001' USING utf8) COLLATE utf8_unicode_ci;
SET @v_actor = CONVERT('SYSTEM' USING utf8) COLLATE utf8_unicode_ci;
SET @v_template_id = CONVERT('DEFAULT' USING utf8) COLLATE utf8_unicode_ci;
SET @v_grid_id = CONVERT('REPORT_PREVIEW' USING utf8) COLLATE utf8_unicode_ci;

SET @v_report_id_asset_book = 68;
SET @v_report_code_asset_book = CONVERT('FA_ASSET_BOOK_REPORT' USING utf8) COLLATE utf8_unicode_ci;
SET @v_menu_code_asset_book = CONVERT('FA_REPORT_ASSET_BOOK' USING utf8) COLLATE utf8_unicode_ci;

SET @v_report_id_depre_period = 69;
SET @v_report_code_depre_period = CONVERT('FA_DEPRECIATION_PERIOD_REPORT' USING utf8) COLLATE utf8_unicode_ci;
SET @v_menu_code_depre_period = CONVERT('FA_REPORT_DEPRECIATION_PERIOD' USING utf8) COLLATE utf8_unicode_ci;

set @v_template_id_depre_period = 27;
set @v_template_id_asset_book = 26;


SET @v_empty = CONVERT('' USING utf8) COLLATE utf8_unicode_ci;

-- =============================================================================
-- A) FA_ASSET_BOOK_REPORT — Sổ tài sản cố định
--    Filters (frontend): useStartYmd, accCd, assetStatus
-- =============================================================================

INSERT INTO sys_report_catalog (
    REPORT_CODE, LABEL_TEXT, CAPTION, REPORT_TYPE, REPORT_SOURCE,
    DATA_SOURCE_TYPE, DATA_SOURCE_REF, DATA_SET_NAME, PARAM_MODE,
    IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_report_code_asset_book,
    @v_report_code_asset_book,
    N'Sổ tài sản cố định',
    'DYNAMIC',
    'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    'STORED_PROCEDURE',
    'CALL rpt_fa_asset_book(@p_COMPANY_CD, @p_USE_START_YMD, @p_LANGUAGE, @p_ACC_CD, @p_ASSET_STATUS)',
    NULL,
    'QUERYSTRING',
    '1', '0', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_report_catalog r
    WHERE r.REPORT_CODE = @v_report_code_asset_book
      AND IFNULL(r.ISDEL, '0') = '0'
);

UPDATE sys_report_catalog
SET LABEL_TEXT = @v_report_code_asset_book,
    CAPTION = N'Sổ tài sản cố định',
    REPORT_TYPE = 'DYNAMIC',
    REPORT_SOURCE = 'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    DATA_SOURCE_TYPE = 'STORED_PROCEDURE',
    DATA_SOURCE_REF = 'CALL rpt_fa_asset_book(@p_COMPANY_CD, @p_USE_START_YMD, @p_LANGUAGE, @p_ACC_CD, @p_ASSET_STATUS)',
    PARAM_MODE = 'QUERYSTRING',
    UPDATED_AT = NOW()
WHERE REPORT_CODE = @v_report_code_asset_book
  AND IFNULL(ISDEL, '0') = '0';

-- Resolve REPORT_ID after insert (prefer catalog id when auto-increment differs)
SELECT @v_report_id_asset_book := REPORT_ID
FROM sys_report_catalog
WHERE REPORT_CODE = @v_report_code_asset_book
  AND IFNULL(ISDEL, '0') = '0'
LIMIT 1;

INSERT INTO company_report_mapping (
    COMPANY_CD, REPORT_KEY, REPORT_ID, SIGN_IDS,
    PAGE_ORIENTATION, PAPER_KIND, FONT_FAMILY,
    FONT_SIZE, TITLE_FONT_SIZE, INFO_FONT_SIZE,
    HEADER_FONT_SIZE, DETAIL_FONT_SIZE, FOOTER_FONT_SIZE,
    MARGIN_LEFT, MARGIN_RIGHT, MARGIN_TOP, MARGIN_BOTTOM,
    IS_DEFAULT, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_company_cd, k.REPORT_KEY, @v_report_id_asset_book, '01,02,03',
    'LANDSCAPE', 'A4', 'Times New Roman',
    8.00, 13.00, 9.00, 8.00, 8.00, 8.00,
    8, 8, 10, 10,
    '1', '1', '0', NOW(), NOW()
FROM (
    SELECT @v_report_code_asset_book AS REPORT_KEY
    UNION ALL
    SELECT @v_menu_code_asset_book
) k
WHERE NOT EXISTS (
    SELECT 1
    FROM company_report_mapping x
    WHERE x.COMPANY_CD = @v_company_cd
      AND x.REPORT_KEY = k.REPORT_KEY
      AND IFNULL(x.ISDEL, '0') = '0'
);

UPDATE company_report_mapping
SET REPORT_ID = @v_report_id_asset_book,
    PAGE_ORIENTATION = 'LANDSCAPE',
    PAPER_KIND = 'A4',
    FONT_FAMILY = 'Times New Roman',
    FONT_SIZE = 8.00,
    TITLE_FONT_SIZE = 13.00,
    INFO_FONT_SIZE = 9.00,
    HEADER_FONT_SIZE = 8.00,
    DETAIL_FONT_SIZE = 8.00,
    FOOTER_FONT_SIZE = 8.00,
    MARGIN_LEFT = 8,
    MARGIN_RIGHT = 8,
    MARGIN_TOP = 10,
    MARGIN_BOTTOM = 10,
    UPDATED_AT = NOW()
WHERE COMPANY_CD = @v_company_cd
  AND REPORT_KEY IN (@v_report_code_asset_book, @v_menu_code_asset_book);

DELETE FROM company_report_element
WHERE REPORT_KEY = @v_report_code_asset_book
  AND IFNULL(COMPANY_CD, @v_empty) IN (@v_empty, @v_company_cd);

INSERT INTO company_report_element (
    COMPANY_CD, REPORT_KEY, SECTION_TYPE, ELEMENT_TYPE, AREA_CODE,
    ITEM_KEY, LABEL_TEXT, CAPTION, VALUE_SOURCE, VALUE_FIELD,
    ROW_NO, COL_NO, COL_SPAN, SORT_ORDER, ALIGN_HEADER, ALIGN_DATA, ALIGN,
    DATA_TYPE, IS_SUMMARY, IS_BOLD, IS_ITALIC, IS_VISIBLE, ISDEL,
    CREATED_AT, UPDATED_AT
) VALUES
(@v_empty, @v_report_code_asset_book, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_NAME', 'COMPANY_NAME', NULL, 'COMPANY', 'COMPANY_NAME', 1, 1, 1, 10, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_asset_book, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_ADDRESS', 'COMPANY_ADDRESS', NULL, 'COMPANY', 'ADDRESS', 2, 1, 1, 20, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_asset_book, 'REPORT_HEADER', 'TEXT', 'FULL', 'TITLE', 'FA_ASSET_BOOK_REPORT', N'SỔ TÀI SẢN CỐ ĐỊNH', 'FIXED_TEXT', NULL, 4, 1, 1, 50, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_asset_book, 'REPORT_HEADER', 'FIELD', 'FULL', 'REPORT_PERIOD', NULL, NULL, 'DATA', 'REPORT_PERIOD_TEXT', 5, 1, 1, 60, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_asset_book, 'REPORT_FOOTER', 'TEXT', 'RIGHT', 'SIGN_DATE', NULL, N'Ngày..... tháng..... năm .....', 'FIXED_TEXT', NULL, 3, 1, 1, 30, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '1', '1', '0', NOW(), NOW());

-- =============================================================================
-- B) FA_DEPRECIATION_PERIOD_REPORT — Bảng khấu hao TSCĐ theo kỳ
--    Filters (frontend): useStartYmd, accCd, assetStatus
-- =============================================================================

INSERT INTO sys_report_catalog (
    REPORT_CODE, LABEL_TEXT, CAPTION, REPORT_TYPE, REPORT_SOURCE,
    DATA_SOURCE_TYPE, DATA_SOURCE_REF, DATA_SET_NAME, PARAM_MODE,
    IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_report_code_depre_period,
    @v_report_code_depre_period,
    N'Bảng khấu hao TSCĐ theo kỳ',
    'DYNAMIC',
    'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    'STORED_PROCEDURE',
    'CALL rpt_fa_depreciation_period(@p_COMPANY_CD, @p_USE_START_YMD, @p_LANGUAGE, @p_ACC_CD, @p_ASSET_STATUS, @p_DEPARTMENT_CD)',
    NULL,
    'QUERYSTRING',
    '1', '0', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_report_catalog r
    WHERE r.REPORT_CODE = @v_report_code_depre_period
      AND IFNULL(r.ISDEL, '0') = '0'
);

UPDATE sys_report_catalog
SET LABEL_TEXT = @v_report_code_depre_period,
    CAPTION = N'Bảng khấu hao TSCĐ theo kỳ',
    REPORT_TYPE = 'DYNAMIC',
    REPORT_SOURCE = 'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    DATA_SOURCE_TYPE = 'STORED_PROCEDURE',
    DATA_SOURCE_REF = 'CALL rpt_fa_depreciation_period(@p_COMPANY_CD, @p_USE_START_YMD, @p_LANGUAGE, @p_ACC_CD, @p_ASSET_STATUS, @p_DEPARTMENT_CD)',
    PARAM_MODE = 'QUERYSTRING',
    UPDATED_AT = NOW()
WHERE REPORT_CODE = @v_report_code_depre_period
  AND IFNULL(ISDEL, '0') = '0';

SELECT @v_report_id_depre_period := REPORT_ID
FROM sys_report_catalog
WHERE REPORT_CODE = @v_report_code_depre_period
  AND IFNULL(ISDEL, '0') = '0'
LIMIT 1;

INSERT INTO company_report_mapping (
    COMPANY_CD, REPORT_KEY, REPORT_ID, SIGN_IDS,
    PAGE_ORIENTATION, PAPER_KIND, FONT_FAMILY,
    FONT_SIZE, TITLE_FONT_SIZE, INFO_FONT_SIZE,
    HEADER_FONT_SIZE, DETAIL_FONT_SIZE, FOOTER_FONT_SIZE,
    MARGIN_LEFT, MARGIN_RIGHT, MARGIN_TOP, MARGIN_BOTTOM,
    IS_DEFAULT, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_company_cd, k.REPORT_KEY, @v_report_id_depre_period, '01,02,03',
    'LANDSCAPE', 'A4', 'Times New Roman',
    8.00, 13.00, 9.00, 8.00, 8.00, 8.00,
    8, 8, 10, 10,
    '1', '1', '0', NOW(), NOW()
FROM (
    SELECT @v_report_code_depre_period AS REPORT_KEY
    UNION ALL
    SELECT @v_menu_code_depre_period
) k
WHERE NOT EXISTS (
    SELECT 1
    FROM company_report_mapping x
    WHERE x.COMPANY_CD = @v_company_cd
      AND x.REPORT_KEY = k.REPORT_KEY
      AND IFNULL(x.ISDEL, '0') = '0'
);

UPDATE company_report_mapping
SET REPORT_ID = @v_report_id_depre_period,
    PAGE_ORIENTATION = 'LANDSCAPE',
    PAPER_KIND = 'A4',
    FONT_FAMILY = 'Times New Roman',
    FONT_SIZE = 8.00,
    TITLE_FONT_SIZE = 13.00,
    INFO_FONT_SIZE = 9.00,
    HEADER_FONT_SIZE = 8.00,
    DETAIL_FONT_SIZE = 8.00,
    FOOTER_FONT_SIZE = 8.00,
    MARGIN_LEFT = 8,
    MARGIN_RIGHT = 8,
    MARGIN_TOP = 10,
    MARGIN_BOTTOM = 10,
    UPDATED_AT = NOW()
WHERE COMPANY_CD = @v_company_cd
  AND REPORT_KEY IN (@v_report_code_depre_period, @v_menu_code_depre_period);

DELETE FROM company_report_element
WHERE REPORT_KEY = @v_report_code_depre_period
  AND IFNULL(COMPANY_CD, @v_empty) IN (@v_empty, @v_company_cd);

INSERT INTO company_report_element (
    COMPANY_CD, REPORT_KEY, SECTION_TYPE, ELEMENT_TYPE, AREA_CODE,
    ITEM_KEY, LABEL_TEXT, CAPTION, VALUE_SOURCE, VALUE_FIELD,
    ROW_NO, COL_NO, COL_SPAN, SORT_ORDER, ALIGN_HEADER, ALIGN_DATA, ALIGN,
    DATA_TYPE, IS_SUMMARY, IS_BOLD, IS_ITALIC, IS_VISIBLE, ISDEL,
    CREATED_AT, UPDATED_AT
) VALUES
(@v_empty, @v_report_code_depre_period, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_NAME', 'COMPANY_NAME', NULL, 'COMPANY', 'COMPANY_NAME', 1, 1, 1, 10, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_depre_period, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_ADDRESS', 'COMPANY_ADDRESS', NULL, 'COMPANY', 'ADDRESS', 2, 1, 1, 20, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_depre_period, 'REPORT_HEADER', 'TEXT', 'FULL', 'TITLE', 'FA_DEPRECIATION_PERIOD_REPORT', N'BẢNG KHẤU HAO TÀI SẢN CỐ ĐỊNH THEO KỲ', 'FIXED_TEXT', NULL, 4, 1, 1, 50, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_depre_period, 'REPORT_HEADER', 'FIELD', 'FULL', 'REPORT_PERIOD', NULL, NULL, 'DATA', 'REPORT_PERIOD_TEXT', 5, 1, 1, 60, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '0', '1', '0', NOW(), NOW()),
(@v_empty, @v_report_code_depre_period, 'REPORT_FOOTER', 'TEXT', 'RIGHT', 'SIGN_DATE', NULL, N'Ngày..... tháng..... năm .....', 'FIXED_TEXT', NULL, 3, 1, 1, 30, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '1', '1', '0', NOW(), NOW());

-- =============================================================================
-- Verify
-- =============================================================================
SELECT m.REPORT_KEY, m.REPORT_ID, r.REPORT_CODE, r.DATA_SOURCE_REF
FROM company_report_mapping m
JOIN sys_report_catalog r ON r.REPORT_ID = m.REPORT_ID
WHERE m.COMPANY_CD = @v_company_cd
  AND m.REPORT_KEY IN (
      @v_report_code_asset_book, @v_menu_code_asset_book,
      @v_report_code_depre_period, @v_menu_code_depre_period
  );

-- =============================================================================
-- C) sys_report_column_layout + sys_grid_column_setting
--    SCREEN_CD = menuCode; GRID_ID = REPORT_PREVIEW
-- =============================================================================

-- ----- FA_ASSET_BOOK_REPORT columns -----
DELETE FROM sys_report_column_layout
WHERE REPORT_CODE = @v_report_code_asset_book
  AND IFNULL(COMPANY_CD, @v_empty) = @v_empty;

INSERT INTO sys_report_column_layout (
    COMPANY_CD, REPORT_CODE, COLUMN_KEY, PARENT_KEY, FIELD_NAME, CAPTION, LABEL_TEXT,
    ROW_INDEX, COL_INDEX, COL_SPAN, ROW_SPAN, WIDTH, ALIGN, FORMAT_TYPE, SORT_ORDER,
    IS_ACTIVE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES
(@v_empty, @v_report_code_asset_book, 'ACC_CD', NULL, 'ACC_CD', N'TK TSCĐ', 'FA_RPT_COL_ACC_CD', 0, 0, 1, 1, 0.55, 'CENTER', 'TEXT', 10, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'ACC_NM', NULL, 'ACC_NM', N'Tên TK', 'FA_RPT_COL_ACC_NM', 0, 1, 1, 1, 0.95, 'LEFT', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'ASSET_CD', NULL, 'ASSET_CD', N'Mã TSCĐ', 'FA_RPT_COL_ASSET_CD', 0, 2, 1, 1, 0.60, 'CENTER', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'ASSET_NM', NULL, 'ASSET_NM', N'Tên TSCĐ', 'FA_RPT_COL_ASSET_NM', 0, 3, 1, 1, 1.20, 'LEFT', 'TEXT', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'USE_DEPT_CD', NULL, 'USE_DEPT_CD', N'Mã BP', 'FA_RPT_COL_USE_DEPT_CD', 0, 4, 1, 1, 0.55, 'CENTER', 'TEXT', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'USE_DEPT_NM', NULL, 'USE_DEPT_NM', N'Bộ phận', 'FA_RPT_COL_USE_DEPT_NM', 0, 5, 1, 1, 0.90, 'LEFT', 'TEXT', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'RECEIVE_YMD', NULL, 'RECEIVE_YMD', N'Ngày nhận', 'FA_RPT_COL_RECEIVE_YMD', 0, 6, 1, 1, 0.70, 'CENTER', 'DATE', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'USE_START_YMD', NULL, 'USE_START_YMD', N'Ngày SD', 'FA_RPT_COL_USE_START_YMD', 0, 7, 1, 1, 0.70, 'CENTER', 'DATE', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'DEPRE_END_YMD', NULL, 'DEPRE_END_YMD', N'Ngày KH cuối', 'FA_RPT_COL_DEPRE_END_YMD', 0, 8, 1, 1, 0.70, 'CENTER', 'DATE', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'USEFUL_LIFE_MONTH', NULL, 'USEFUL_LIFE_MONTH', N'Số tháng KH', 'FA_RPT_COL_USEFUL_LIFE_MONTH', 0, 9, 1, 1, 0.50, 'CENTER', 'INTEGER', 100, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'ORIGINAL_AMT', NULL, 'ORIGINAL_AMT', N'Nguyên giá', 'FA_RPT_COL_ORIGINAL_AMT', 0, 10, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 110, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'OPENING_ACCUM_DEPRE_AMT', NULL, 'OPENING_ACCUM_DEPRE_AMT', N'LK KH đầu kỳ', 'FA_RPT_COL_OPENING_ACCUM_DEPRE_AMT', 0, 11, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 120, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'OPENING_BOOK_AMT', NULL, 'OPENING_BOOK_AMT', N'GTCL đầu kỳ', 'FA_RPT_COL_OPENING_BOOK_AMT', 0, 12, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 130, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'PERIOD_DEPRE_AMT', NULL, 'PERIOD_DEPRE_AMT', N'KH trong kỳ', 'FA_RPT_COL_PERIOD_DEPRE_AMT', 0, 13, 1, 1, 0.80, 'RIGHT', 'NUMBER0', 140, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'ACCUM_DEPRE_AMT', NULL, 'ACCUM_DEPRE_AMT', N'LK KH lũy kế', 'FA_RPT_COL_ACCUM_DEPRE_AMT', 0, 14, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 150, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'END_BOOK_AMT', NULL, 'END_BOOK_AMT', N'GTCL cuối kỳ', 'FA_RPT_COL_END_BOOK_AMT', 0, 15, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 160, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'REMAIN_MONTHS_PERIOD', NULL, 'REMAIN_MONTHS_PERIOD', N'Tháng còn KH', 'FA_RPT_COL_REMAIN_MONTHS_PERIOD', 0, 16, 1, 1, 0.55, 'CENTER', 'INTEGER', 170, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_asset_book, 'ASSET_STATUS', NULL, 'ASSET_STATUS', N'Trạng thái', 'FA_RPT_COL_ASSET_STATUS', 0, 17, 1, 1, 0.60, 'CENTER', 'TEXT', 180, '1', '0', @v_actor, NOW(), @v_actor, NOW());

DELETE FROM sys_grid_column_setting
WHERE TEMPLATE_ID = @v_template_id_asset_book
  AND GRID_ID = @v_grid_id;

INSERT INTO sys_grid_column_setting (
    TEMPLATE_ID, GRID_ID, FIELD_NAME, LABEL_TEXT, CAPTION,
    IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH,
    IS_FIXED, FIXED_POSITION, ALLOW_HIDING, SORT_ORDER, SORT_INDEX,
    CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT, ISDEL
) VALUES
(@v_template_id_asset_book, @v_grid_id, 'ACC_CD', 'FA_RPT_COL_ACC_CD', N'TK TSCĐ', '1', 0, 90, '0', NULL, '1', '10', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'ACC_NM', 'FA_RPT_COL_ACC_NM', N'Tên TK', '1', 1, 160, '0', NULL, '1', '20', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'ASSET_CD', 'FA_RPT_COL_ASSET_CD', N'Mã TSCĐ', '1', 2, 100, '0', NULL, '1', '30', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'ASSET_NM', 'FA_RPT_COL_ASSET_NM', N'Tên TSCĐ', '1', 3, 200, '0', NULL, '1', '40', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'USE_DEPT_CD', 'FA_RPT_COL_USE_DEPT_CD', N'Mã BP', '1', 4, 90, '0', NULL, '1', '50', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'USE_DEPT_NM', 'FA_RPT_COL_USE_DEPT_NM', N'Bộ phận', '1', 5, 150, '0', NULL, '1', '60', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'RECEIVE_YMD', 'FA_RPT_COL_RECEIVE_YMD', N'Ngày nhận', '1', 6, 110, '0', NULL, '1', '70', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'USE_START_YMD', 'FA_RPT_COL_USE_START_YMD', N'Ngày SD', '1', 7, 110, '0', NULL, '1', '80', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'DEPRE_END_YMD', 'FA_RPT_COL_DEPRE_END_YMD', N'Ngày KH cuối', '1', 8, 110, '0', NULL, '1', '90', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'USEFUL_LIFE_MONTH', 'FA_RPT_COL_USEFUL_LIFE_MONTH', N'Số tháng KH', '1', 9, 90, '0', NULL, '1', '100', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'ORIGINAL_AMT', 'FA_RPT_COL_ORIGINAL_AMT', N'Nguyên giá', '1', 10, 120, '0', NULL, '1', '110', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'OPENING_ACCUM_DEPRE_AMT', 'FA_RPT_COL_OPENING_ACCUM_DEPRE_AMT', N'LK KH đầu kỳ', '1', 11, 120, '0', NULL, '1', '120', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'OPENING_BOOK_AMT', 'FA_RPT_COL_OPENING_BOOK_AMT', N'GTCL đầu kỳ', '1', 12, 120, '0', NULL, '1', '130', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'PERIOD_DEPRE_AMT', 'FA_RPT_COL_PERIOD_DEPRE_AMT', N'KH trong kỳ', '1', 13, 110, '0', NULL, '1', '140', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'ACCUM_DEPRE_AMT', 'FA_RPT_COL_ACCUM_DEPRE_AMT', N'LK KH lũy kế', '1', 14, 120, '0', NULL, '1', '150', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'END_BOOK_AMT', 'FA_RPT_COL_END_BOOK_AMT', N'GTCL cuối kỳ', '1', 15, 120, '0', NULL, '1', '160', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'REMAIN_MONTHS_PERIOD', 'FA_RPT_COL_REMAIN_MONTHS_PERIOD', N'Tháng còn KH', '1', 16, 90, '0', NULL, '1', '170', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_asset_book, @v_grid_id, 'ASSET_STATUS', 'FA_RPT_COL_ASSET_STATUS', N'Trạng thái', '1', 17, 100, '0', NULL, '1', '180', 0, @v_actor, NOW(), @v_actor, NOW(), '0');

-- ----- FA_DEPRECIATION_PERIOD_REPORT columns (legacy field names) -----
DELETE FROM sys_report_column_layout
WHERE REPORT_CODE = @v_report_code_depre_period
  AND IFNULL(COMPANY_CD, @v_empty) = @v_empty;

INSERT INTO sys_report_column_layout (
    COMPANY_CD, REPORT_CODE, COLUMN_KEY, PARENT_KEY, FIELD_NAME, CAPTION, LABEL_TEXT,
    ROW_INDEX, COL_INDEX, COL_SPAN, ROW_SPAN, WIDTH, ALIGN, FORMAT_TYPE, SORT_ORDER,
    IS_ACTIVE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES
(@v_empty, @v_report_code_depre_period, 'FA_ACC_CD', NULL, 'FA_ACC_CD', N'TK TSCĐ', 'FA_RPT_COL_ACC_CD', 0, 0, 1, 1, 0.55, 'CENTER', 'TEXT', 10, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'FA_ACC_NM', NULL, 'FA_ACC_NM', N'Tên TK', 'FA_RPT_COL_ACC_NM', 0, 1, 1, 1, 0.95, 'LEFT', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'FADEP_ACC_CD', NULL, 'FADEP_ACC_CD', N'TK Nợ KH', 'FA_RPT_COL_DEBIT_ACCT_CD', 0, 2, 1, 1, 0.55, 'CENTER', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'FAEX_ACC_CD', NULL, 'FAEX_ACC_CD', N'TK Có KH', 'FA_RPT_COL_CREDIT_ACCT_CD', 0, 3, 1, 1, 0.55, 'CENTER', 'TEXT', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'FA_CD', NULL, 'FA_CD', N'Mã TSCĐ', 'FA_RPT_COL_ASSET_CD', 0, 4, 1, 1, 0.60, 'CENTER', 'TEXT', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'FA_NM', NULL, 'FA_NM', N'Tên TSCĐ', 'FA_RPT_COL_ASSET_NM', 0, 5, 1, 1, 1.20, 'LEFT', 'TEXT', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'DEPARTMENT_CD', NULL, 'DEPARTMENT_CD', N'Mã BP', 'FA_RPT_COL_USE_DEPT_CD', 0, 6, 1, 1, 0.55, 'CENTER', 'TEXT', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'DEPARTMENT_NM', NULL, 'DEPARTMENT_NM', N'Bộ phận', 'FA_RPT_COL_USE_DEPT_NM', 0, 7, 1, 1, 0.90, 'LEFT', 'TEXT', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'CHIT_NO', NULL, 'CHIT_NO', N'Số CT', 'FA_RPT_COL_CHIT_NO', 0, 8, 1, 1, 0.60, 'CENTER', 'TEXT', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'ACQ_YMD', NULL, 'ACQ_YMD', N'Ngày nhận', 'FA_RPT_COL_RECEIVE_YMD', 0, 9, 1, 1, 0.70, 'CENTER', 'DATE', 100, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'USE_YMD', NULL, 'USE_YMD', N'Ngày SD', 'FA_RPT_COL_USE_START_YMD', 0, 10, 1, 1, 0.70, 'CENTER', 'DATE', 110, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'DEP_MONTH', NULL, 'DEP_MONTH', N'Số tháng KH', 'FA_RPT_COL_USEFUL_LIFE_MONTH', 0, 11, 1, 1, 0.50, 'CENTER', 'INTEGER', 120, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'REMAIN_TERM', NULL, 'REMAIN_TERM', N'Tháng còn KH', 'FA_RPT_COL_REMAIN_MONTHS_PERIOD', 0, 12, 1, 1, 0.55, 'CENTER', 'INTEGER', 130, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'ACQ_MONEY', NULL, 'ACQ_MONEY', N'Nguyên giá', 'FA_RPT_COL_ORIGINAL_AMT', 0, 13, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 140, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'PRE_PERIOD_ACCUMONEY', NULL, 'PRE_PERIOD_ACCUMONEY', N'LK KH đầu năm', 'FA_RPT_COL_OPENING_ACCUM_DEPRE_AMT', 0, 14, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 150, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'PRIOR_REMAINMONEY', NULL, 'PRIOR_REMAINMONEY', N'GTCL đầu năm', 'FA_RPT_COL_OPENING_BOOK_AMT', 0, 15, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 160, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'DEPMONTHMONEY', NULL, 'DEPMONTHMONEY', N'KH tháng', 'FA_RPT_COL_PERIOD_DEPRE_AMT', 0, 16, 1, 1, 0.80, 'RIGHT', 'NUMBER0', 170, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'PERIOD_ACCUMONEY', NULL, 'PERIOD_ACCUMONEY', N'LK KH trong năm', 'FA_RPT_COL_PERIOD_ACCUM_DEPRE_AMT', 0, 17, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 180, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'ACCUMONEY', NULL, 'ACCUMONEY', N'LK KH lũy kế', 'FA_RPT_COL_ACCUM_DEPRE_AMT', 0, 18, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 190, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'REMAINMONEY', NULL, 'REMAINMONEY', N'GTCL cuối kỳ', 'FA_RPT_COL_END_BOOK_AMT', 0, 19, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 200, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_empty, @v_report_code_depre_period, 'STATE', NULL, 'STATE', N'Trạng thái', 'FA_RPT_COL_ASSET_STATUS', 0, 20, 1, 1, 0.60, 'CENTER', 'TEXT', 210, '1', '0', @v_actor, NOW(), @v_actor, NOW());



DELETE FROM sys_grid_column_setting
WHERE TEMPLATE_ID = @v_template_id_depre_period
  AND GRID_ID = @v_grid_id;

INSERT INTO sys_grid_column_setting (
    TEMPLATE_ID, GRID_ID, FIELD_NAME, LABEL_TEXT, CAPTION,
    IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH,
    IS_FIXED, FIXED_POSITION, ALLOW_HIDING, SORT_ORDER, SORT_INDEX,
    CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT, ISDEL
) VALUES
(@v_template_id_depre_period, @v_grid_id, 'FA_ACC_CD', 'FA_RPT_COL_ACC_CD', N'TK TSCĐ', '1', 0, 90, '0', NULL, '1', '10', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'FA_ACC_NM', 'FA_RPT_COL_ACC_NM', N'Tên TK', '1', 1, 160, '0', NULL, '1', '20', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'FADEP_ACC_CD', 'FA_RPT_COL_DEBIT_ACCT_CD', N'TK Nợ KH', '1', 2, 90, '0', NULL, '1', '30', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'FAEX_ACC_CD', 'FA_RPT_COL_CREDIT_ACCT_CD', N'TK Có KH', '1', 3, 90, '0', NULL, '1', '40', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'FA_CD', 'FA_RPT_COL_ASSET_CD', N'Mã TSCĐ', '1', 4, 100, '0', NULL, '1', '50', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'FA_NM', 'FA_RPT_COL_ASSET_NM', N'Tên TSCĐ', '1', 5, 200, '0', NULL, '1', '60', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'DEPARTMENT_CD', 'FA_RPT_COL_USE_DEPT_CD', N'Mã BP', '1', 6, 90, '0', NULL, '1', '70', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'DEPARTMENT_NM', 'FA_RPT_COL_USE_DEPT_NM', N'Bộ phận', '1', 7, 150, '0', NULL, '1', '80', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'CHIT_NO', 'FA_RPT_COL_CHIT_NO', N'Số CT', '1', 8, 100, '0', NULL, '1', '90', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'ACQ_YMD', 'FA_RPT_COL_RECEIVE_YMD', N'Ngày nhận', '1', 9, 110, '0', NULL, '1', '100', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'USE_YMD', 'FA_RPT_COL_USE_START_YMD', N'Ngày SD', '1', 10, 110, '0', NULL, '1', '110', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'DEP_MONTH', 'FA_RPT_COL_USEFUL_LIFE_MONTH', N'Số tháng KH', '1', 11, 90, '0', NULL, '1', '120', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'REMAIN_TERM', 'FA_RPT_COL_REMAIN_MONTHS_PERIOD', N'Tháng còn KH', '1', 12, 90, '0', NULL, '1', '130', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'ACQ_MONEY', 'FA_RPT_COL_ORIGINAL_AMT', N'Nguyên giá', '1', 13, 120, '0', NULL, '1', '140', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'PRE_PERIOD_ACCUMONEY', 'FA_RPT_COL_OPENING_ACCUM_DEPRE_AMT', N'LK KH đầu năm', '1', 14, 120, '0', NULL, '1', '150', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'PRIOR_REMAINMONEY', 'FA_RPT_COL_OPENING_BOOK_AMT', N'GTCL đầu năm', '1', 15, 120, '0', NULL, '1', '160', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'DEPMONTHMONEY', 'FA_RPT_COL_PERIOD_DEPRE_AMT', N'KH tháng', '1', 16, 110, '0', NULL, '1', '170', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'PERIOD_ACCUMONEY', 'FA_RPT_COL_PERIOD_ACCUM_DEPRE_AMT', N'LK KH trong năm', '1', 17, 120, '0', NULL, '1', '180', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'ACCUMONEY', 'FA_RPT_COL_ACCUM_DEPRE_AMT', N'LK KH lũy kế', '1', 18, 120, '0', NULL, '1', '190', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'REMAINMONEY', 'FA_RPT_COL_END_BOOK_AMT', N'GTCL cuối kỳ', '1', 19, 120, '0', NULL, '1', '200', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_template_id_depre_period, @v_grid_id, 'STATE', 'FA_RPT_COL_ASSET_STATUS', N'Trạng thái', '1', 20, 100, '0', NULL, '1', '210', 0, @v_actor, NOW(), @v_actor, NOW(), '0');
-- =============================================================================
-- Menu DB (am_web_manager) — thêm thủ công nếu chưa có
--    FA_REPORT_ASSET_BOOK           → /fa/report/asset-book
--    FA_REPORT_DEPRECIATION_PERIOD  → /fa/report/depreciation-period
--    PARENT: cùng parent FA_REPORT_DEPRECIATION
-- Chi tiết: 01_fa_report_menu_seed.sql
-- =============================================================================
-- Company DB: deploy SP qua 00_install_all.sql
--   rpt_fa_asset_book.sql
--   rpt_fa_depreciation_period.sql
-- =============================================================================
