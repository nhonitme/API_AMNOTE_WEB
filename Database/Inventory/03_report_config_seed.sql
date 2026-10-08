-- =============================================================================
-- INVENTORY_OPENING_INFO — PDF report catalog (master grid / DEFAULT_GRID)
-- Chạy trên DB manager (am_web_manager).
-- SP rpt_inventory_opening_info phải deploy trước trên company DB
--   (xem 02_sp_inventory_opening.sql).
--
-- Tham chiếu: master/warehouse-type
--   REPORT_CODE = WAREHOUSE_TYPE_INFO
--   MENU_CODE   = MD_WAREHOUSE_TYPE
--
-- Inventory Opening:
--   REPORT_CODE = INVENTORY_OPENING_INFO
--   MENU_CODE   = INV_OPENING
--   ROUTE_PATH  = /inventory/opening
--   GRID_ID     = inventory-opening-grid (print dùng sys_grid_column_setting trang)
-- =============================================================================

SET @v_company_cd = '0001';
SET @v_report_code = 'INVENTORY_OPENING_INFO';
SET @v_menu_code = 'INV_OPENING';
SET @v_actor = 'SYSTEM';
SET @v_data_source_ref = 'CALL rpt_inventory_opening_info(@p_COMPANY_CD, @p_INPUT_ID, @p_LANGUAGE)';

-- =============================================================================
-- 1) sys_report_catalog
-- =============================================================================
INSERT INTO sys_report_catalog (
    REPORT_CODE, REPORT_NAME, REPORT_TYPE, REPORT_SOURCE,
    DATA_SOURCE_TYPE, DATA_SOURCE_REF, DATA_SET_NAME, PARAM_MODE,
    DESCRIPTION, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
    @v_report_code,
    N'Tồn đầu kỳ kho',
    'DYNAMIC',
    'API_AMNOTE_WEB.Reporting.DynamicConfiguredReport',
    'STORED_PROCEDURE',
    @v_data_source_ref,
    @v_report_code,
    'QUERYSTRING',
    N'Inventory opening (IRO) master grid report',
    '1', '0', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_report_catalog r WHERE r.REPORT_CODE = @v_report_code AND IFNULL(r.ISDEL, '0') = '0'
);

UPDATE sys_report_catalog
SET DATA_SOURCE_REF = @v_data_source_ref,
    REPORT_NAME = N'Tồn đầu kỳ kho',
    REPORT_TYPE = 'DYNAMIC',
    DATA_SOURCE_TYPE = 'STORED_PROCEDURE',
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
-- 3) company_report_element — header/footer (REPORT_KEY = menuCode, như MD_WAREHOUSE_TYPE)
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
('', @v_menu_code, 'REPORT_HEADER', 'TEXT', 'FULL', 'TITLE', 'INV_OPENING_TITLE', N'TỒN ĐẦU KỲ KHO', 'FIXED_TEXT', NULL, 3, 1, 1, 30, 'CENTER', 'CENTER', 'CENTER', 'TEXT', '0', '1', '0', '1', '0', NOW(), NOW()),
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
(@v_company_cd, @v_report_code, 'PRODUCT_CD', NULL, 'PRODUCT_CD', N'Mã hàng', 'PRODUCT_CD', 0, 1, 1, 1, 1.20, 'LEFT', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'PRODUCT_NM_VIET', NULL, 'PRODUCT_NM_VIET', N'Tên hàng', 'PRODUCT_NM_VIET', 0, 2, 1, 1, 2.40, 'LEFT', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'STORE_CD', NULL, 'STORE_CD', N'Mã kho', 'STORE_CD', 0, 3, 1, 1, 1.00, 'LEFT', 'TEXT', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'STORE_NM_VIET', NULL, 'STORE_NM_VIET', N'Tên kho', 'STORE_NM_VIET', 0, 4, 1, 1, 2.00, 'LEFT', 'TEXT', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'UNIT_NM', NULL, 'UNIT_NM', N'Đơn vị', 'UNIT_NM', 0, 5, 1, 1, 0.90, 'LEFT', 'TEXT', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'QUANTITY', NULL, 'QUANTITY', N'Số lượng', 'QUANTITY', 0, 6, 1, 1, 1.00, 'RIGHT', 'NUMBER3', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'UNIT_PRICE_CC', NULL, 'UNIT_PRICE_CC', N'Đơn giá', 'UNIT_PRICE_CC', 0, 7, 1, 1, 1.10, 'RIGHT', 'NUMBER2', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'AMOUNT_CC', NULL, 'AMOUNT_CC', N'Thành tiền', 'AMOUNT_CC', 0, 8, 1, 1, 1.20, 'RIGHT', 'NUMBER2', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_report_code, 'SUMMARY', NULL, 'SUMMARY', N'Ghi chú', 'SUMMARY', 0, 9, 1, 1, 1.80, 'LEFT', 'TEXT', 100, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'ROW_NO', NULL, 'ROW_NO', 'STT', 'RPT_COL_SEQ', 0, 0, 1, 1, 0.60, 'CENTER', 'INTEGER', 10, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'PRODUCT_CD', NULL, 'PRODUCT_CD', N'Mã hàng', 'PRODUCT_CD', 0, 1, 1, 1, 1.20, 'LEFT', 'TEXT', 20, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'PRODUCT_NM_VIET', NULL, 'PRODUCT_NM_VIET', N'Tên hàng', 'PRODUCT_NM_VIET', 0, 2, 1, 1, 2.40, 'LEFT', 'TEXT', 30, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'STORE_CD', NULL, 'STORE_CD', N'Mã kho', 'STORE_CD', 0, 3, 1, 1, 1.00, 'LEFT', 'TEXT', 40, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'STORE_NM_VIET', NULL, 'STORE_NM_VIET', N'Tên kho', 'STORE_NM_VIET', 0, 4, 1, 1, 2.00, 'LEFT', 'TEXT', 50, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'UNIT_NM', NULL, 'UNIT_NM', N'Đơn vị', 'UNIT_NM', 0, 5, 1, 1, 0.90, 'LEFT', 'TEXT', 60, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'QUANTITY', NULL, 'QUANTITY', N'Số lượng', 'QUANTITY', 0, 6, 1, 1, 1.00, 'RIGHT', 'NUMBER3', 70, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'UNIT_PRICE_CC', NULL, 'UNIT_PRICE_CC', N'Đơn giá', 'UNIT_PRICE_CC', 0, 7, 1, 1, 1.10, 'RIGHT', 'NUMBER2', 80, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'AMOUNT_CC', NULL, 'AMOUNT_CC', N'Thành tiền', 'AMOUNT_CC', 0, 8, 1, 1, 1.20, 'RIGHT', 'NUMBER2', 90, '1', '0', @v_actor, NOW(), @v_actor, NOW()),
(@v_company_cd, @v_menu_code, 'SUMMARY', NULL, 'SUMMARY', N'Ghi chú', 'SUMMARY', 0, 9, 1, 1, 1.80, 'LEFT', 'TEXT', 100, '1', '0', @v_actor, NOW(), @v_actor, NOW());

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
