-- AMNote / Excel Import TẤT CẢ thành phẩm
-- Chạy trên DB quản lý (am_manager), KHÔNG chạy trên DB công ty.
-- Đổi 0001 thành COMPANY_CD cần cấu hình. Tương thích MySQL 5.6.
-- Dùng chung BaseExcelImportPopup / excel_template_column, moduleCd = ProductMaterialInfoAll.
-- Thành phẩm và NVL được tra qua product_info theo COMPANY_CD khi import.
-- Không tạo bảng BOM hoặc stored procedure nhập riêng: tái sử dụng sp_product_material_info_upsert.

SET @p_COMPANY_CD = '0001';

INSERT INTO excel_template_column
  (COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT, CAPTION,
   TABLE_NM, EXPLAIN_TABLE_QUERY, DATA_TYPE, IS_PRIMARY_KEY, IS_REQUIRED)
SELECT @p_COMPANY_CD, 'ProductMaterialInfoAll', 'PRODUCT_CD',
       'Mã thành phẩm', 'Mã thành phẩm', NULL, NULL, 'string', '0', '1'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM excel_template_column
  WHERE COMPANY_CD = @p_COMPANY_CD
    AND MODULE_CD = 'ProductMaterialInfoAll'
    AND FIELD_NAME = 'PRODUCT_CD'
);

INSERT INTO excel_template_column
  (COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT, CAPTION,
   TABLE_NM, EXPLAIN_TABLE_QUERY, DATA_TYPE, IS_PRIMARY_KEY, IS_REQUIRED)
SELECT @p_COMPANY_CD, 'ProductMaterialInfoAll', 'MATERIAL_PRODUCT_CD',
       'Mã NVL', 'Mã NVL', NULL, NULL, 'string', '0', '1'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM excel_template_column
  WHERE COMPANY_CD = @p_COMPANY_CD
    AND MODULE_CD = 'ProductMaterialInfoAll'
    AND FIELD_NAME = 'MATERIAL_PRODUCT_CD'
);

INSERT INTO excel_template_column
  (COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT, CAPTION,
   TABLE_NM, EXPLAIN_TABLE_QUERY, DATA_TYPE, IS_PRIMARY_KEY, IS_REQUIRED)
SELECT @p_COMPANY_CD, 'ProductMaterialInfoAll', 'QUANTITY',
       'Số lượng định mức', 'Số lượng định mức', NULL, NULL, 'decimal', '0', '1'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM excel_template_column
  WHERE COMPANY_CD = @p_COMPANY_CD
    AND MODULE_CD = 'ProductMaterialInfoAll'
    AND FIELD_NAME = 'QUANTITY'
);

SELECT COMPANY_CD, MODULE_CD, FIELD_NAME, LABEL_TEXT,
       CAPTION, DATA_TYPE, IS_REQUIRED
FROM excel_template_column
WHERE COMPANY_CD = @p_COMPANY_CD
  AND MODULE_CD = 'ProductMaterialInfoAll'
ORDER BY ID;

-- Nếu metadata đang được cache, xóa cache Excel template hoặc restart API.
-- Backend cũng khai báo 3 cột mặc định trong ExcelTemplateDefaults.
