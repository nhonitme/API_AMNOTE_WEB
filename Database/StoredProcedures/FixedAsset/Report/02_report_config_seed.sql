-- =============================================================================
-- FA_DEPRECIATION_REPORT — fix mapping + seed PDF layout + grid columns
-- Chạy trên DB manager (am_web_manager).
-- SP rpt_fa_depreciation phải deploy trước trên company DB.
--
-- Tham chiếu cấu trúc: Database/StoredProcedures/FixedAsset/Report/sys_report.sql
-- REPORT_ID tham chiếu (sys_report_catalog):
--   42 = GL_CASHFLOW_B03DN_TT  (rpt_cashflow_b03dn_direct)
--   67 = FA_DEPRECIATION_REPORT (rpt_fa_depreciation)
-- =============================================================================

SET @v_company_cd = '0001';
SET @v_report_id_fa = 67;
SET @v_report_id_cashflow_tt = 42;
SET @v_report_code = 'FA_DEPRECIATION_REPORT';
SET @v_menu_code = 'FA_REPORT_DEPRECIATION';
-- sys_grid_column_setting.SCREEN_CD = menuCode (giống GL_BOOK_AR_AGING, không dùng reportCode)
SET @v_screen_cd = 'FA_REPORT_DEPRECIATION';
SET @v_grid_id = 'REPORT_PREVIEW';
SET @v_template_id = 'DEFAULT';
SET @v_actor = 'SYSTEM';

-- =============================================================================
-- 0) FIX company_report_mapping (đảo nhầm REPORT_ID 42 <-> 67)
-- =============================================================================

UPDATE company_report_mapping
SET REPORT_KEY = @v_report_code,
    REPORT_ID = @v_report_id_fa,
    UPDATED_AT = NOW()
WHERE COMPANY_CD = @v_company_cd
  AND TRIM(REPORT_KEY) = @v_report_code
  AND REPORT_ID = @v_report_id_cashflow_tt;

INSERT INTO company_report_mapping (
    COMPANY_CD, REPORT_KEY, REPORT_ID, SIGN_IDS,
    PAGE_ORIENTATION, PAPER_KIND, FONT_FAMILY,
    FONT_SIZE, TITLE_FONT_SIZE, INFO_FONT_SIZE,
    HEADER_FONT_SIZE, DETAIL_FONT_SIZE, FOOTER_FONT_SIZE,
    MARGIN_LEFT, MARGIN_RIGHT, MARGIN_TOP, MARGIN_BOTTOM,
    IS_DEFAULT, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_company_cd, @v_menu_code, @v_report_id_fa, m.SIGN_IDS,
    m.PAGE_ORIENTATION, m.PAPER_KIND, m.FONT_FAMILY,
    m.FONT_SIZE, m.TITLE_FONT_SIZE, m.INFO_FONT_SIZE,
    m.HEADER_FONT_SIZE, m.DETAIL_FONT_SIZE, m.FOOTER_FONT_SIZE,
    m.MARGIN_LEFT, m.MARGIN_RIGHT, m.MARGIN_TOP, m.MARGIN_BOTTOM,
    '1', '1', '0', NOW(), NOW()
FROM company_report_mapping m
WHERE m.COMPANY_CD = @v_company_cd
  AND m.REPORT_KEY = @v_report_code
  AND m.REPORT_ID = @v_report_id_fa
  AND NOT EXISTS (
      SELECT 1
      FROM company_report_mapping x
      WHERE x.COMPANY_CD = @v_company_cd
        AND x.REPORT_KEY = @v_menu_code
        AND IFNULL(x.ISDEL, '0') = '0'
  )
LIMIT 1;

-- Landscape cho 19 cột
UPDATE company_report_mapping
SET PAGE_ORIENTATION = 'LANDSCAPE',
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
  AND REPORT_KEY IN (@v_report_code, @v_menu_code)
  AND REPORT_ID = @v_report_id_fa;

-- =============================================================================
-- 1) sys_report_catalog (chỉ khi chưa có)
-- =============================================================================
INSERT INTO sys_report_catalog (
    REPORT_CODE, LABEL_TEXT, CAPTION, REPORT_TYPE, REPORT_SOURCE,
    DATA_SOURCE_TYPE, DATA_SOURCE_REF, DATA_SET_NAME, PARAM_MODE,
    IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_report_code,
    @v_report_code,
    N'Bảng tính khấu hao',
    'DYNAMIC',
    'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    'STORED_PROCEDURE',
    'CALL rpt_fa_depreciation(@p_COMPANY_CD, @p_USE_START_YMD, @p_LANGUAGE, @p_ACC_CD, @p_ASSET_STATUS)',
    NULL,
    'QUERYSTRING',
    '1', '0', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_report_catalog r WHERE r.REPORT_CODE = @v_report_code AND IFNULL(r.ISDEL, '0') = '0'
);

UPDATE sys_report_catalog
SET LABEL_TEXT = @v_report_code,
    CAPTION = N'Bảng tính khấu hao',
    DATA_SOURCE_REF = 'CALL rpt_fa_depreciation(@p_COMPANY_CD, @p_USE_START_YMD, @p_LANGUAGE, @p_ACC_CD, @p_ASSET_STATUS)',
    PARAM_MODE = 'QUERYSTRING',
    UPDATED_AT = NOW()
WHERE REPORT_CODE = @v_report_code
  AND IFNULL(ISDEL, '0') = '0';

-- =============================================================================
-- 2) company_report_element — header/footer PDF BOOK
-- =============================================================================
DELETE FROM company_report_element
WHERE REPORT_KEY = @v_report_code
  AND IFNULL(COMPANY_CD, '') IN ('', @v_company_cd);

INSERT INTO company_report_element (
    COMPANY_CD, REPORT_KEY, SECTION_TYPE, ELEMENT_TYPE, AREA_CODE,
    ITEM_KEY, LABEL_TEXT, CAPTION, VALUE_SOURCE, VALUE_FIELD,
    ROW_NO, COL_NO, COL_SPAN, SORT_ORDER, ALIGN_HEADER, ALIGN_DATA, ALIGN,
    DATA_TYPE, IS_SUMMARY, IS_BOLD, IS_ITALIC, IS_VISIBLE, ISDEL,
    CREATED_AT, UPDATED_AT
) VALUES
('', @v_report_code, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_NAME', 'COMPANY_NAME', NULL, 'COMPANY', 'COMPANY_NAME', 1, 1, 1, 10, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
('', @v_report_code, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_ADDRESS', 'COMPANY_ADDRESS', NULL, 'COMPANY', 'ADDRESS', 2, 1, 1, 20, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
('', @v_report_code, 'REPORT_HEADER', 'TEXT', 'FULL', 'TITLE', 'FA_DEPRECIATION_REPORT', N'BẢNG TÍNH KHẤU HAO TÀI SẢN CỐ ĐỊNH', 'FIXED_TEXT', NULL, 4, 1, 1, 50, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
('', @v_report_code, 'REPORT_HEADER', 'FIELD', 'FULL', 'REPORT_PERIOD', NULL, NULL, 'DATA', 'REPORT_PERIOD_TEXT', 5, 1, 1, 60, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '0', '1', '0', NOW(), NOW()),
('', @v_report_code, 'REPORT_FOOTER', 'TEXT', 'RIGHT', 'SIGN_DATE', NULL, N'Ngày..... tháng..... năm .....', 'FIXED_TEXT', NULL, 3, 1, 1, 30, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '1', '1', '0', NOW(), NOW());

-- =============================================================================
-- 3) sys_report_column_layout — 19 cột (copy/paste chạy trực tiếp)
--    Pattern: flat ROW_INDEX = 0 (giống INVENTORY_SOURCE_DOCUMENT_REPORT)
--    Lưu ý: chỉnh ID 668–686 nếu DB đã có ID trùng (max hiện tại + 1)
-- =============================================================================
DELETE FROM `sys_report_column_layout`
WHERE `REPORT_CODE` = 'FA_DEPRECIATION_REPORT'
  AND IFNULL(`COMPANY_CD`, '') = '';

INSERT INTO `sys_report_column_layout` (`ID`, `COMPANY_CD`, `REPORT_CODE`, `COLUMN_KEY`, `PARENT_KEY`, `FIELD_NAME`, `CAPTION`, `LABEL_TEXT`, `ROW_INDEX`, `COL_INDEX`, `COL_SPAN`, `ROW_SPAN`, `WIDTH`, `ALIGN`, `FORMAT_TYPE`, `SORT_ORDER`, `IS_ACTIVE`, `ISDEL`, `CREATE_BY`, `CREATE_AT`, `UPDATE_BY`, `UPDATE_AT`) VALUES
	(668, '', 'FA_DEPRECIATION_REPORT', 'ACC_CD', NULL, 'ACC_CD', 'TK TSCĐ', 'FA_RPT_COL_ACC_CD', 0, 0, 1, 1, 0.55, 'CENTER', 'TEXT', 10, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(669, '', 'FA_DEPRECIATION_REPORT', 'ACC_NM', NULL, 'ACC_NM', 'Tên TK TSCĐ', 'FA_RPT_COL_ACC_NM', 0, 1, 1, 1, 0.95, 'LEFT', 'TEXT', 20, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(670, '', 'FA_DEPRECIATION_REPORT', 'CREDIT_ACCT_CD', NULL, 'CREDIT_ACCT_CD', 'TK Có KH', 'FA_RPT_COL_CREDIT_ACCT_CD', 0, 2, 1, 1, 0.55, 'CENTER', 'TEXT', 30, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(671, '', 'FA_DEPRECIATION_REPORT', 'CREDIT_ACCT_NM', NULL, 'CREDIT_ACCT_NM', 'Tên TK Có KH', 'FA_RPT_COL_CREDIT_ACCT_NM', 0, 3, 1, 1, 0.85, 'LEFT', 'TEXT', 40, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(672, '', 'FA_DEPRECIATION_REPORT', 'DEBIT_ACCT_CD', NULL, 'DEBIT_ACCT_CD', 'TK Nợ KH', 'FA_RPT_COL_DEBIT_ACCT_CD', 0, 4, 1, 1, 0.55, 'CENTER', 'TEXT', 50, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(673, '', 'FA_DEPRECIATION_REPORT', 'DEBIT_ACCT_NM', NULL, 'DEBIT_ACCT_NM', 'Tên TK Nợ KH', 'FA_RPT_COL_DEBIT_ACCT_NM', 0, 5, 1, 1, 0.85, 'LEFT', 'TEXT', 60, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(674, '', 'FA_DEPRECIATION_REPORT', 'ASSET_CD', NULL, 'ASSET_CD', 'Mã TSCĐ', 'FA_RPT_COL_ASSET_CD', 0, 6, 1, 1, 0.60, 'CENTER', 'TEXT', 70, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(675, '', 'FA_DEPRECIATION_REPORT', 'ASSET_NM', NULL, 'ASSET_NM', 'Tên TSCĐ', 'FA_RPT_COL_ASSET_NM', 0, 7, 1, 1, 1.10, 'LEFT', 'TEXT', 80, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(676, '', 'FA_DEPRECIATION_REPORT', 'RECEIVE_YMD', NULL, 'RECEIVE_YMD', 'Ngày nhận', 'FA_RPT_COL_RECEIVE_YMD', 0, 8, 1, 1, 0.70, 'CENTER', 'DATE', 90, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(677, '', 'FA_DEPRECIATION_REPORT', 'USE_START_YMD', NULL, 'USE_START_YMD', 'Ngày SD', 'FA_RPT_COL_USE_START_YMD', 0, 9, 1, 1, 0.70, 'CENTER', 'DATE', 100, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(678, '', 'FA_DEPRECIATION_REPORT', 'DEPRE_END_YMD', NULL, 'DEPRE_END_YMD', 'Ngày KH cuối', 'FA_RPT_COL_DEPRE_END_YMD', 0, 10, 1, 1, 0.70, 'CENTER', 'DATE', 110, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(679, '', 'FA_DEPRECIATION_REPORT', 'REMAIN_MONTHS_TODAY', NULL, 'REMAIN_MONTHS_TODAY', 'Tháng còn KH (HT)', 'FA_RPT_COL_REMAIN_MONTHS_TODAY', 0, 11, 1, 1, 0.55, 'CENTER', 'INTEGER', 120, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(680, '', 'FA_DEPRECIATION_REPORT', 'REMAIN_MONTHS_PERIOD', NULL, 'REMAIN_MONTHS_PERIOD', 'Tháng còn KH (BC)', 'FA_RPT_COL_REMAIN_MONTHS_PERIOD', 0, 12, 1, 1, 0.55, 'CENTER', 'INTEGER', 130, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(681, '', 'FA_DEPRECIATION_REPORT', 'ORIGINAL_AMT', NULL, 'ORIGINAL_AMT', 'Nguyên giá', 'FA_RPT_COL_ORIGINAL_AMT', 0, 13, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 140, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(682, '', 'FA_DEPRECIATION_REPORT', 'OPENING_BOOK_AMT', NULL, 'OPENING_BOOK_AMT', 'GTCL đầu năm', 'FA_RPT_COL_OPENING_BOOK_AMT', 0, 14, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 150, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(683, '', 'FA_DEPRECIATION_REPORT', 'PERIOD_DEPRE_AMT', NULL, 'PERIOD_DEPRE_AMT', 'KH trong kỳ', 'FA_RPT_COL_PERIOD_DEPRE_AMT', 0, 15, 1, 1, 0.80, 'RIGHT', 'NUMBER0', 160, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(684, '', 'FA_DEPRECIATION_REPORT', 'PERIOD_ACCUM_DEPRE_AMT', NULL, 'PERIOD_ACCUM_DEPRE_AMT', 'LK KH trong kỳ', 'FA_RPT_COL_PERIOD_ACCUM_DEPRE_AMT', 0, 16, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 170, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(685, '', 'FA_DEPRECIATION_REPORT', 'ACCUM_DEPRE_AMT', NULL, 'ACCUM_DEPRE_AMT', 'LK KH lũy kế', 'FA_RPT_COL_ACCUM_DEPRE_AMT', 0, 17, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 180, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02'),
	(686, '', 'FA_DEPRECIATION_REPORT', 'END_BOOK_AMT', NULL, 'END_BOOK_AMT', 'GTCL cuối kỳ', 'FA_RPT_COL_END_BOOK_AMT', 0, 18, 1, 1, 0.85, 'RIGHT', 'NUMBER0', 190, '1', '0', 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02');

-- =============================================================================
-- 4) sys_grid_column_setting — preview grid (REPORT_PREVIEW)
--    SCREEN_CD = @v_screen_cd (= menuCode FA_REPORT_DEPRECIATION, giống GL_BOOK_AR_AGING)
--    Lưu ý: chỉnh ID 687–705 nếu DB đã có ID trùng (max hiện tại + 1)
-- =============================================================================
DELETE FROM `sys_grid_column_setting`
WHERE `SCREEN_CD` IN (@v_screen_cd, @v_report_code)
  AND `GRID_ID` = @v_grid_id
  AND `TEMPLATE_ID` = @v_template_id
  AND IFNULL(`COMPANY_CD`, '') IN ('', @v_company_cd)
  AND IFNULL(`USER_ID`, '') = '';

INSERT INTO `sys_grid_column_setting` (`ID`, `COMPANY_CD`, `USER_ID`, `SCREEN_CD`, `GRID_ID`, `TEMPLATE_ID`, `TEMPLATE_NAME`, `IS_DEFAULT_TEMPLATE`, `COLUMN_NAME`, `COLUMN_CAPTION`, `IS_VISIBLE`, `VISIBLE_INDEX`, `COLUMN_WIDTH`, `IS_FIXED`, `FIXED_POSITION`, `ALLOW_HIDING`, `SORT_ORDER`, `SORT_INDEX`, `CREATE_BY`, `CREATE_AT`, `UPDATE_BY`, `UPDATE_AT`, `ISDEL`) VALUES
	(687, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'ACC_CD', N'TK TSCĐ', '1', 0, 90, '0', '', '1', '10', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(688, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'ACC_NM', N'Tên TK TSCĐ', '1', 1, 160, '0', '', '1', '20', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(689, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'CREDIT_ACCT_CD', N'TK Có KH', '1', 2, 90, '0', '', '1', '30', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(690, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'CREDIT_ACCT_NM', N'Tên TK Có KH', '1', 3, 150, '0', '', '1', '40', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(691, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'DEBIT_ACCT_CD', N'TK Nợ KH', '1', 4, 90, '0', '', '1', '50', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(692, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'DEBIT_ACCT_NM', N'Tên TK Nợ KH', '1', 5, 150, '0', '', '1', '60', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(693, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'ASSET_CD', N'Mã TSCĐ', '1', 6, 100, '0', '', '1', '70', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(694, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'ASSET_NM', N'Tên TSCĐ', '1', 7, 200, '0', '', '1', '80', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(695, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'RECEIVE_YMD', N'Ngày nhận', '1', 8, 110, '0', '', '1', '90', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(696, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'USE_START_YMD', N'Ngày sử dụng', '1', 9, 110, '0', '', '1', '100', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(697, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'DEPRE_END_YMD', N'Ngày KH cuối', '1', 10, 110, '0', '', '1', '110', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(698, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'REMAIN_MONTHS_TODAY', N'Tháng còn KH (HT)', '1', 11, 90, '0', '', '1', '120', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(699, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'REMAIN_MONTHS_PERIOD', N'Tháng còn KH (BC)', '1', 12, 90, '0', '', '1', '130', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(700, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'ORIGINAL_AMT', N'Nguyên giá', '1', 13, 120, '0', '', '1', '140', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(701, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'OPENING_BOOK_AMT', N'GTCL đầu năm', '1', 14, 120, '0', '', '1', '150', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(702, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'PERIOD_DEPRE_AMT', N'KH trong kỳ', '1', 15, 110, '0', '', '1', '160', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(703, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'PERIOD_ACCUM_DEPRE_AMT', N'LK KH trong kỳ', '1', 16, 120, '0', '', '1', '170', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(704, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'ACCUM_DEPRE_AMT', N'LK KH lũy kế', '1', 17, 120, '0', '', '1', '180', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0'),
	(705, '0001', '', 'FA_REPORT_DEPRECIATION', 'REPORT_PREVIEW', 'DEFAULT', 'Default', '1', 'END_BOOK_AMT', N'GTCL cuối kỳ', '1', 18, 120, '0', '', '1', '190', 0, 'SYSTEM', '2026-06-03 15:29:02', 'SYSTEM', '2026-06-03 15:29:02', '0');

-- =============================================================================
-- 6) Menu DB (am_web_manager) — thêm thủ công nếu chưa có
--    MENU_CODE = FA_REPORT_DEPRECIATION
--    ROUTE_PATH = /fa/report/depreciation
--    PARENT: menu Báo cáo TSCĐ (FA > Report)
--
--    Các báo cáo FA khác (xem 01_fa_report_menu_seed.sql / 03_fa_asset_book_period_seed.sql):
--    FA_REPORT_ASSET_BOOK          → /fa/report/asset-book
--    FA_REPORT_DEPRECIATION_PERIOD → /fa/report/depreciation-period
-- =============================================================================
SELECT m.ID, m.REPORT_KEY, m.REPORT_ID, r.REPORT_CODE, r.DATA_SOURCE_REF
FROM company_report_mapping m
JOIN sys_report_catalog r ON r.REPORT_ID = m.REPORT_ID
WHERE m.COMPANY_CD = @v_company_cd
  AND m.REPORT_KEY IN (@v_report_code, @v_menu_code, 'GL_CASHFLOW_B03DN_TT');

SELECT REPORT_CODE, COLUMN_KEY, FIELD_NAME, CAPTION, LABEL_TEXT, COL_INDEX, SORT_ORDER
FROM sys_report_column_layout
WHERE REPORT_CODE = @v_report_code
  AND IFNULL(ISDEL, '0') = '0'
ORDER BY SORT_ORDER;

SELECT SCREEN_CD, COLUMN_NAME, COLUMN_CAPTION, VISIBLE_INDEX, COLUMN_WIDTH, SORT_ORDER
FROM sys_grid_column_setting
WHERE SCREEN_CD = @v_screen_cd
  AND GRID_ID = @v_grid_id
  AND TEMPLATE_ID = @v_template_id
  AND IFNULL(ISDEL, '0') = '0'
ORDER BY VISIBLE_INDEX;
