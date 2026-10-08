-- =============================================================================
-- TAX_VAT_INOUT_LIST — seed PDF layout + grid columns
-- Chạy trên DB manager (am_web_manager).
-- SP rpt_vat_inout_list phải deploy trước trên company DB.
--
-- Tham chiếu cấu trúc: Database/StoredProcedures/FixedAsset/Report/02_report_config_seed.sql
--   MENU_CODE = TAX_VAT_INOUT_LIST
--   ROUTE_PATH = /tax/vat/inout-list
-- =============================================================================

SET @v_company_cd = '0001';
SET @v_report_code = 'TAX_VAT_INOUT_LIST';
SET @v_menu_code = 'TAX_VAT_INOUT_LIST';
SET @v_screen_cd = 'TAX_VAT_INOUT_LIST';
SET @v_grid_id = 'REPORT_PREVIEW';
SET @v_template_id = 'DEFAULT';
SET @v_actor = 'SYSTEM';
SET @v_data_source_ref = 'CALL rpt_vat_inout_list(@p_COMPANY_CD, @p_FROM_DATE, @p_TO_DATE, @p_TYPE, @p_STATUS, @p_SEARCH_TEXT, @p_LANGUAGE)';

-- =============================================================================
-- 1) sys_report_catalog (bảng dump) + sys_report (nếu môi trường dùng tên này)
-- =============================================================================
INSERT INTO sys_report_catalog (
    REPORT_CODE, REPORT_NAME, REPORT_TYPE, REPORT_SOURCE,
    DATA_SOURCE_TYPE, DATA_SOURCE_REF, DATA_SET_NAME, PARAM_MODE,
    DESCRIPTION, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_report_code,
    N'Danh sách hóa đơn đầu vào/đầu ra',
    'DYNAMIC',
    'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    'STORED_PROCEDURE',
    @v_data_source_ref,
    @v_report_code,
    'QUERYSTRING',
    N'Danh sách hóa đơn VAT đầu vào/đầu ra',
    '1', '0', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_report_catalog r WHERE r.REPORT_CODE = @v_report_code AND IFNULL(r.ISDEL, '0') = '0'
);

UPDATE sys_report_catalog
SET DATA_SOURCE_REF = @v_data_source_ref,
    REPORT_NAME = N'Danh sách hóa đơn đầu vào/đầu ra',
    UPDATED_AT = NOW()
WHERE REPORT_CODE = @v_report_code
  AND IFNULL(ISDEL, '0') = '0';

SET @v_report_id = (
    SELECT REPORT_ID
    FROM sys_report_catalog
    WHERE REPORT_CODE = @v_report_code
      AND IFNULL(ISDEL, '0') = '0'
    LIMIT 1
);

-- =============================================================================
-- 2) company_report_mapping — REPORT_KEY = reportCode và menuCode
-- =============================================================================
INSERT INTO company_report_mapping (
    COMPANY_CD, REPORT_KEY, REPORT_ID, SIGN_IDS,
    PAGE_ORIENTATION, PAPER_KIND, FONT_FAMILY,
    FONT_SIZE, TITLE_FONT_SIZE, INFO_FONT_SIZE,
    HEADER_FONT_SIZE, DETAIL_FONT_SIZE, FOOTER_FONT_SIZE,
    MARGIN_LEFT, MARGIN_RIGHT, MARGIN_TOP, MARGIN_BOTTOM,
    IS_DEFAULT, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_company_cd, mapping_key.REPORT_KEY, @v_report_id, m.SIGN_IDS,
    'LANDSCAPE', 'A4', 'Times New Roman',
    8.00, 13.00, 9.00,
    8.00, 8.00, 8.00,
    8, 8, 10, 10,
    '1', '1', '0', NOW(), NOW()
FROM (
    SELECT @v_report_code AS REPORT_KEY
    UNION ALL
    SELECT @v_menu_code
) AS mapping_key
CROSS JOIN (
    SELECT SIGN_IDS
    FROM company_report_mapping
    WHERE COMPANY_CD = @v_company_cd
      AND IFNULL(ISDEL, '0') = '0'
    ORDER BY ID
    LIMIT 1
) AS m
WHERE @v_report_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM company_report_mapping x
      WHERE x.COMPANY_CD = @v_company_cd
        AND x.REPORT_KEY = mapping_key.REPORT_KEY
        AND IFNULL(x.ISDEL, '0') = '0'
  );

UPDATE company_report_mapping
SET REPORT_ID = @v_report_id,
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
  AND REPORT_KEY IN (@v_report_code, @v_menu_code)
  AND @v_report_id IS NOT NULL;

-- =============================================================================
-- 3) company_report_element — header/footer PDF BOOK
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
('', @v_report_code, 'REPORT_HEADER', 'TEXT', 'FULL', 'TITLE', 'TAX_VAT_INOUT_LIST', N'DANH SÁCH HÓA ĐƠN ĐẦU VÀO/ĐẦU RA', 'FIXED_TEXT', NULL, 4, 1, 1, 50, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
('', @v_report_code, 'REPORT_HEADER', 'FIELD', 'FULL', 'REPORT_PERIOD', NULL, NULL, 'DATA', 'REPORT_PERIOD_TEXT', 5, 1, 1, 60, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '0', '1', '0', NOW(), NOW()),
('', @v_report_code, 'REPORT_FOOTER', 'TEXT', 'RIGHT', 'SIGN_DATE', NULL, N'Ngày..... tháng..... năm .....', 'FIXED_TEXT', NULL, 3, 1, 1, 30, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '1', '1', '0', NOW(), NOW());

-- =============================================================================
-- 4) sys_report_column_layout
-- =============================================================================
DELETE FROM sys_report_column_layout
WHERE REPORT_CODE = @v_report_code
  AND IFNULL(COMPANY_CD, '') = '';

INSERT INTO sys_report_column_layout (
    COMPANY_CD, REPORT_CODE, COLUMN_KEY, PARENT_KEY, FIELD_NAME, CAPTION, LABEL_TEXT,
    ROW_INDEX, COL_INDEX, COL_SPAN, ROW_SPAN, WIDTH, ALIGN, FORMAT_TYPE, SORT_ORDER,
    IS_ACTIVE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES
('', @v_report_code, 'TTHAI_TEN', NULL, 'TTHAI_TEN', 'Trạng thái hóa đơn', 'TTHAI', 0, 0, 1, 1, 0.90, 'LEFT', 'TEXT', 10, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'KHMSHDON', NULL, 'KHMSHDON', 'Mẫu số hóa đơn', 'KHMSHDON', 0, 1, 1, 1, 0.60, 'CENTER', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'KHHDON', NULL, 'KHHDON', 'Ký hiệu hóa đơn', 'KHHDON', 0, 2, 1, 1, 0.65, 'CENTER', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'TDLAP_TEN', NULL, 'TDLAP_TEN', 'Ngày lập hóa đơn', 'TDLAP', 0, 3, 1, 1, 0.70, 'CENTER', 'DATE', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'SHDON', NULL, 'SHDON', 'Số hóa đơn', 'SHDON', 0, 4, 1, 1, 0.65, 'CENTER', 'TEXT', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'MST', NULL, 'MST', 'Mã số thuế', 'MST', 0, 5, 1, 1, 0.75, 'CENTER', 'TEXT', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'TEN', NULL, 'TEN', 'Tên đối tác', 'TEN', 0, 6, 1, 1, 1.40, 'LEFT', 'TEXT', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'DCHI', NULL, 'DCHI', 'Địa chỉ', 'DCHI', 0, 7, 1, 1, 1.50, 'LEFT', 'TEXT', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'DVTTE', NULL, 'DVTTE', 'Loại tiền', 'DVTTE', 0, 8, 1, 1, 0.45, 'CENTER', 'TEXT', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'TGIA', NULL, 'TGIA', 'Tỷ giá', 'TGIA', 0, 9, 1, 1, 0.55, 'RIGHT', 'NUMBER4', 100, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'TGTCTHUE', NULL, 'TGTCTHUE', 'Tiền trước thuế', 'TGTCTHUE', 0, 10, 1, 1, 0.80, 'RIGHT', 'NUMBER2', 110, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'TGTTHUE', NULL, 'TGTTHUE', 'Tiền thuế', 'TGTTHUE', 0, 11, 1, 1, 0.70, 'RIGHT', 'NUMBER2', 120, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'TGTTTBSO', NULL, 'TGTTTBSO', 'Tổng tiền', 'TGTTTBSO', 0, 12, 1, 1, 0.80, 'RIGHT', 'NUMBER2', 130, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'GCHU', NULL, 'GCHU', 'Ghi chú', 'GCHU', 0, 13, 1, 1, 0.90, 'LEFT', 'TEXT', 140, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'CHIT_NO', NULL, 'CHIT_NO', 'Số chứng từ kế toán liên kết', 'CHIT_NO', 0, 14, 1, 1, 0.85, 'CENTER', 'TEXT', 150, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
('', @v_report_code, 'CHIT_YMD', NULL, 'CHIT_YMD', 'Ngày chứng từ kế toán', 'CHIT_YMD', 0, 15, 1, 1, 0.75, 'CENTER', 'DATE', 160, '1', '0', @v_actor, NOW(), @v_actor, NOW());

-- =============================================================================
-- 5) sys_grid_column_setting — preview grid (REPORT_PREVIEW)
--    SCREEN_CD = menuCode TAX_VAT_INOUT_LIST
-- =============================================================================
DELETE FROM sys_grid_column_setting
WHERE SCREEN_CD IN (@v_screen_cd, @v_report_code)
  AND GRID_ID = @v_grid_id
  AND TEMPLATE_ID = @v_template_id
  AND IFNULL(COMPANY_CD, '') IN ('', @v_company_cd)
  AND IFNULL(USER_ID, '') = '';

INSERT INTO sys_grid_column_setting (
    COMPANY_CD, USER_ID, SCREEN_CD, GRID_ID, TEMPLATE_ID, TEMPLATE_NAME, IS_DEFAULT_TEMPLATE,
    COLUMN_NAME, COLUMN_CAPTION, IS_VISIBLE, VISIBLE_INDEX, COLUMN_WIDTH, IS_FIXED, FIXED_POSITION,
    ALLOW_HIDING, SORT_ORDER, SORT_INDEX, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT, ISDEL
) VALUES
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TTHAI_TEN', N'Trạng thái hóa đơn', '1', 0, 160, '0', '', '1', '10', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'KHMSHDON', N'Mẫu số hóa đơn', '1', 1, 110, '0', '', '1', '20', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'KHMSHDON_TEN', N'Tên loại hóa đơn', '0', 2, 160, '0', '', '0', '25', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'KHHDON', N'Ký hiệu hóa đơn', '1', 2, 120, '0', '', '1', '30', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TDLAP_TEN', N'Ngày lập hóa đơn', '1', 3, 130, '0', '', '1', '40', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'SHDON', N'Số hóa đơn', '1', 4, 120, '0', '', '1', '50', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'MST', N'Mã số thuế', '1', 5, 130, '0', '', '1', '60', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TEN', N'Tên đối tác', '1', 6, 220, '0', '', '1', '70', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'DCHI', N'Địa chỉ', '1', 7, 240, '0', '', '1', '80', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'DVTTE', N'Loại tiền', '1', 8, 90, '0', '', '1', '90', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TGIA', N'Tỷ giá', '1', 9, 100, '0', '', '1', '100', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TGTCTHUE', N'Tiền trước thuế', '1', 10, 140, '0', '', '1', '110', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TGTTHUE', N'Tiền thuế', '1', 11, 130, '0', '', '1', '120', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'TGTTTBSO', N'Tổng tiền', '1', 12, 140, '0', '', '1', '130', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'GCHU', N'Ghi chú', '1', 13, 180, '0', '', '1', '140', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'CHIT_NO', N'Số chứng từ kế toán liên kết', '1', 14, 170, '0', '', '1', '150', 0, @v_actor, NOW(), @v_actor, NOW(), '0'),
(@v_company_cd, '', @v_screen_cd, @v_grid_id, @v_template_id, 'Default', '1', 'CHIT_YMD', N'Ngày chứng từ kế toán', '1', 15, 150, '0', '', '1', '160', 0, @v_actor, NOW(), @v_actor, NOW(), '0');

-- =============================================================================
-- 6) Menu DB (am_web_manager) — thêm thủ công nếu chưa có
--    MENU_CODE = TAX_VAT_INOUT_LIST
--    ROUTE_PATH = /tax/vat/inout-list
-- =============================================================================
SELECT m.ID, m.REPORT_KEY, m.REPORT_ID, r.REPORT_CODE, r.DATA_SOURCE_REF
FROM company_report_mapping m
JOIN sys_report_catalog r ON r.REPORT_ID = m.REPORT_ID
WHERE m.COMPANY_CD = @v_company_cd
  AND m.REPORT_KEY IN (@v_report_code, @v_menu_code);

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
