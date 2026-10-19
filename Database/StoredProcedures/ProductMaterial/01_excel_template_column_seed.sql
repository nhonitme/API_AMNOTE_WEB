-- Run in am_manager (MANAGER database), NOT in a company database.
-- Replace 0001 with the target COMPANY_CD before running.
-- The two columns map to the shared BaseExcelImportPopup module ProductMaterialInfo.
-- PRODUCT_ID is selected on screen and submitted separately as productId.
-- Compatible with MySQL 5.6; safe to rerun without duplicating rows.

SET @p_COMPANY_CD = '0001';

INSERT INTO excel_template_column
  (COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT, CAPTION,
   TABLE_NM, EXPLAIN_TABLE_QUERY, DATA_TYPE, IS_PRIMARY_KEY, IS_REQUIRED)
SELECT @p_COMPANY_CD, 'ProductMaterialInfo', 'MATERIAL_PRODUCT_CD',
       'Mã NVL', 'Mã NVL', NULL, NULL, 'string', '0', '1'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM excel_template_column
  WHERE COMPANY_CD = @p_COMPANY_CD
    AND MODULE_CD = 'ProductMaterialInfo'
    AND FIELD_NAME = 'MATERIAL_PRODUCT_CD'
);

INSERT INTO excel_template_column
  (COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT, CAPTION,
   TABLE_NM, EXPLAIN_TABLE_QUERY, DATA_TYPE, IS_PRIMARY_KEY, IS_REQUIRED)
SELECT @p_COMPANY_CD, 'ProductMaterialInfo', 'QUANTITY',
       'Số lượng định mức', 'Số lượng định mức', NULL, NULL, 'decimal', '0', '1'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM excel_template_column
  WHERE COMPANY_CD = @p_COMPANY_CD
    AND MODULE_CD = 'ProductMaterialInfo'
    AND FIELD_NAME = 'QUANTITY'
);

-- Check registered columns:
SELECT COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT, CAPTION,
       DATA_TYPE, IS_PRIMARY_KEY, IS_REQUIRED
FROM excel_template_column
WHERE COMPANY_CD = @p_COMPANY_CD AND MODULE_CD = 'ProductMaterialInfo'
ORDER BY ID;

-- If template columns were cached, clear the Excel-template cache or restart the API.
-- API also includes a built-in ProductMaterialInfo template fallback for unseeded companies.
