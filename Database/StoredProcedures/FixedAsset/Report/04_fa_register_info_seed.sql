-- =============================================================================
-- FIXED_ASSET_INFO — PDF report catalog (master grid / DEFAULT_GRID)
-- Chạy trên DB manager (am_web_manager).
-- SP rpt_fixed_asset_info phải deploy trước trên company DB
--   (xem rpt_fixed_asset_info.sql).
--
-- Tham chiếu: master/warehouse
--   REPORT_CODE = WAREHOUSE_INFO
--   MENU_CODE   = MD_WAREHOUSE
--
-- Fixed Asset Register:
--   REPORT_CODE = FIXED_ASSET_INFO
--   MENU_CODE   = FA_REGISTER
--   ROUTE_PATH  = /fa/register
--   GRID_ID     = fixed-asset-grid (print dùng sys_grid_column_setting trang)
-- =============================================================================

SET @v_company_cd = CONVERT('0001' USING utf8) COLLATE utf8_unicode_ci;
SET @v_report_code = CONVERT('FIXED_ASSET_INFO' USING utf8) COLLATE utf8_unicode_ci;
SET @v_menu_code = CONVERT('FA_REGISTER' USING utf8) COLLATE utf8_unicode_ci;
SET @v_actor = CONVERT('SYSTEM' USING utf8) COLLATE utf8_unicode_ci;
SET @v_data_source_ref = 'CALL rpt_fixed_asset_info(@p_COMPANY_CD, @p_ASSET_ID, @p_ASSET_CD, @p_LANGUAGE)';

-- =============================================================================
-- 1) sys_report_catalog
-- Schema thực tế (am_web_manager): LABEL_TEXT + CAPTION (không còn REPORT_NAME/DESCRIPTION)
-- Tham chiếu WAREHOUSE_INFO: REPORT_TYPE = MASTER_GRID
-- =============================================================================
INSERT INTO sys_report_catalog (
    REPORT_CODE, LABEL_TEXT, CAPTION, REPORT_TYPE, REPORT_SOURCE,
    DATA_SOURCE_TYPE, DATA_SOURCE_REF, DATA_SET_NAME, PARAM_MODE,
    IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_report_code,
    'REPORT_FIXED_ASSET_INFO',
    N'Danh mục tài sản cố định',
    'MASTER_GRID',
    'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    'STORED_PROCEDURE',
    @v_data_source_ref,
    NULL,
    'QUERYSTRING',
    '1', '0', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_report_catalog r WHERE r.REPORT_CODE = @v_report_code AND IFNULL(r.ISDEL, '0') = '0'
);

UPDATE sys_report_catalog
SET LABEL_TEXT = 'REPORT_FIXED_ASSET_INFO',
    CAPTION = N'Danh mục tài sản cố định',
    REPORT_TYPE = 'MASTER_GRID',
    REPORT_SOURCE = 'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    DATA_SOURCE_TYPE = 'STORED_PROCEDURE',
    DATA_SOURCE_REF = @v_data_source_ref,
    PARAM_MODE = 'QUERYSTRING',
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
    'LANDSCAPE', 'A4', 'Arial',
    8.50, 16.00, 9.00,
    8.50, 8.50, 8.00,
    40, 40, 25, 25,
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
    FONT_FAMILY = 'Arial',
    FONT_SIZE = 8.50,
    TITLE_FONT_SIZE = 16.00,
    INFO_FONT_SIZE = 9.00,
    HEADER_FONT_SIZE = 8.50,
    DETAIL_FONT_SIZE = 8.50,
    FOOTER_FONT_SIZE = 8.00,
    MARGIN_LEFT = 40,
    MARGIN_RIGHT = 40,
    MARGIN_TOP = 25,
    MARGIN_BOTTOM = 25,
    UPDATED_AT = NOW()
WHERE COMPANY_CD = @v_company_cd
  AND REPORT_KEY IN (@v_report_code, @v_menu_code)
  AND @v_report_id IS NOT NULL;

-- =============================================================================
-- 3) company_report_element — header/footer (REPORT_KEY = menuCode)
-- =============================================================================
DELETE FROM company_report_element
WHERE REPORT_KEY IN (@v_report_code, @v_menu_code)
  AND IFNULL(COMPANY_CD, '') IN ('', @v_company_cd);

INSERT INTO company_report_element (
    COMPANY_CD, REPORT_KEY, SECTION_TYPE, ELEMENT_TYPE, AREA_CODE,
    ITEM_KEY, LABEL_TEXT, CAPTION, VALUE_SOURCE, VALUE_FIELD,
    ROW_NO, COL_NO, COL_SPAN, SORT_ORDER, ALIGN_HEADER, ALIGN_DATA, ALIGN,
    DATA_TYPE, IS_SUMMARY, IS_BOLD, IS_ITALIC, IS_VISIBLE, ISDEL,
    CREATED_AT, UPDATED_AT
) VALUES
('', @v_menu_code, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_NAME', 'COMPANY_NAME', NULL, 'COMPANY', 'COMPANY_NAME', 1, 1, 1, 10, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
('', @v_menu_code, 'REPORT_HEADER', 'FIELD', 'LEFT', 'COMPANY_ADDRESS', 'COMPANY_ADDRESS', NULL, 'COMPANY', 'ADDRESS', 2, 1, 1, 20, 'LEFT', 'LEFT', 'LEFT', 'TEXT', '0', '0', '0', '1', '0', NOW(), NOW()),
('', @v_menu_code, 'REPORT_HEADER', 'TEXT', 'FULL', 'TITLE', 'FA_REGISTER_TITLE', N'DANH MỤC TÀI SẢN CỐ ĐỊNH', 'FIXED_TEXT', NULL, 3, 1, 1, 30, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
('', @v_menu_code, 'REPORT_FOOTER', 'TEXT', 'RIGHT', 'SIGN_DATE', 'SIGN_DATE', N'Ngày..... tháng.... năm......', 'FIXED_TEXT', NULL, 1, 1, 1, 10, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '0', '1', '1', '0', NOW(), NOW());

-- =============================================================================
-- 4) sys_report_column_layout (fallback khi không có preview grid settings)
-- =============================================================================
DELETE FROM sys_report_column_layout
WHERE REPORT_CODE IN (@v_report_code, @v_menu_code)
  AND IFNULL(COMPANY_CD, '') IN ('', @v_company_cd);

INSERT INTO sys_report_column_layout (
    COMPANY_CD, REPORT_CODE, COLUMN_KEY, PARENT_KEY, FIELD_NAME, CAPTION, LABEL_TEXT,
    ROW_INDEX, COL_INDEX, COL_SPAN, ROW_SPAN, WIDTH, ALIGN, FORMAT_TYPE, SORT_ORDER,
    IS_ACTIVE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES
(@v_company_cd, @v_report_code, 'ROW_NO', NULL, 'ROW_NO', 'STT', 'RPT_COL_SEQ', 0, 0, 1, 1, 0.60, 'CENTER', 'INTEGER', 10, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'ASSET_CD', NULL, 'ASSET_CD', N'Mã tài sản', 'ASSET_CD', 0, 1, 1, 1, 1.30, 'LEFT', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'ASSET_NM', NULL, 'ASSET_NM', N'Tên tài sản', 'ASSET_NM', 0, 2, 1, 1, 2.40, 'LEFT', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'ACC_CD', NULL, 'ACC_CD', N'Mã tài khoản', 'ACC_CD', 0, 3, 1, 1, 1.10, 'LEFT', 'TEXT', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'USE_START_YMD', NULL, 'USE_START_YMD', N'Ngày sử dụng', 'USE_START_YMD', 0, 4, 1, 1, 1.20, 'CENTER', 'DATE', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'ORIGINAL_AMT', NULL, 'ORIGINAL_AMT', N'Nguyên giá', 'ORIGINAL_AMT', 0, 5, 1, 1, 1.30, 'RIGHT', 'NUMBER2', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'ACCUM_DEPRE_AMT', NULL, 'ACCUM_DEPRE_AMT', N'HM lũy kế', 'ACCUM_DEPRE_AMT', 0, 6, 1, 1, 1.30, 'RIGHT', 'NUMBER2', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'REMAIN_DEPRE_AMT', NULL, 'REMAIN_DEPRE_AMT', N'GT còn lại KH', 'REMAIN_DEPRE_AMT', 0, 7, 1, 1, 1.30, 'RIGHT', 'NUMBER2', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'STATUS', NULL, 'STATUS', N'Trạng thái', 'STATUS', 0, 8, 1, 1, 1.10, 'LEFT', 'TEXT', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ROW_NO', NULL, 'ROW_NO', 'STT', 'RPT_COL_SEQ', 0, 0, 1, 1, 0.60, 'CENTER', 'INTEGER', 10, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ASSET_CD', NULL, 'ASSET_CD', N'Mã tài sản', 'ASSET_CD', 0, 1, 1, 1, 1.30, 'LEFT', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ASSET_NM', NULL, 'ASSET_NM', N'Tên tài sản', 'ASSET_NM', 0, 2, 1, 1, 2.40, 'LEFT', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ACC_CD', NULL, 'ACC_CD', N'Mã tài khoản', 'ACC_CD', 0, 3, 1, 1, 1.10, 'LEFT', 'TEXT', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'USE_START_YMD', NULL, 'USE_START_YMD', N'Ngày sử dụng', 'USE_START_YMD', 0, 4, 1, 1, 1.20, 'CENTER', 'DATE', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ORIGINAL_AMT', NULL, 'ORIGINAL_AMT', N'Nguyên giá', 'ORIGINAL_AMT', 0, 5, 1, 1, 1.30, 'RIGHT', 'NUMBER2', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ACCUM_DEPRE_AMT', NULL, 'ACCUM_DEPRE_AMT', N'HM lũy kế', 'ACCUM_DEPRE_AMT', 0, 6, 1, 1, 1.30, 'RIGHT', 'NUMBER2', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'REMAIN_DEPRE_AMT', NULL, 'REMAIN_DEPRE_AMT', N'GT còn lại KH', 'REMAIN_DEPRE_AMT', 0, 7, 1, 1, 1.30, 'RIGHT', 'NUMBER2', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'STATUS', NULL, 'STATUS', N'Trạng thái', 'STATUS', 0, 8, 1, 1, 1.10, 'LEFT', 'TEXT', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW());

-- =============================================================================
-- Verify
-- =============================================================================
SELECT m.ID, m.REPORT_KEY, m.REPORT_ID, r.REPORT_CODE, r.DATA_SOURCE_REF
FROM company_report_mapping m
JOIN sys_report_catalog r ON r.REPORT_ID = m.REPORT_ID
WHERE m.COMPANY_CD = @v_company_cd
  AND m.REPORT_KEY IN (@v_report_code, @v_menu_code);

SELECT REPORT_CODE, COLUMN_KEY, FIELD_NAME, CAPTION, COL_INDEX, SORT_ORDER
FROM sys_report_column_layout
WHERE REPORT_CODE IN (@v_report_code, @v_menu_code)
  AND IFNULL(ISDEL, '0') = '0'
ORDER BY REPORT_CODE, SORT_ORDER;
