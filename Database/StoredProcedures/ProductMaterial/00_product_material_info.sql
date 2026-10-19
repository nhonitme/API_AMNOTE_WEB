-- AMNote - Product material master (MySQL 5.6 compatible)
-- Execute in EACH company database (for example am_web_001).
-- No BOM version; no price or wastage. COMPANY_CD is always validated.
-- Existing table is preserved by CREATE TABLE IF NOT EXISTS.
-- Naming: sp_{table_or_module}_{action}, parameters p_{DB_COLUMN}.

CREATE TABLE IF NOT EXISTS product_material_info (
  MATERIAL_ID BIGINT(20) NOT NULL AUTO_INCREMENT COMMENT 'ID dòng liên kết',
  COMPANY_CD VARCHAR(20) COLLATE utf8_unicode_ci NOT NULL DEFAULT '' COMMENT 'Mã công ty',
  PRODUCT_ID BIGINT(20) NOT NULL COMMENT 'ID thành phẩm trong product_info',
  MATERIAL_PRODUCT_ID BIGINT(20) NOT NULL COMMENT 'ID nguyên vật liệu trong product_info',
  QUANTITY DECIMAL(18,6) NOT NULL DEFAULT '0' COMMENT 'Số lượng NVL cần dùng',
  ISDEL CHAR(1) COLLATE utf8_unicode_ci DEFAULT '0' COMMENT '1: đã xoá',
  CREATE_BY VARCHAR(20) COLLATE utf8_unicode_ci DEFAULT NULL,
  CREATE_AT TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
  UPDATE_BY VARCHAR(20) COLLATE utf8_unicode_ci DEFAULT NULL,
  UPDATE_AT TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (MATERIAL_ID),
  KEY IDX_COMPANY_PRODUCT (COMPANY_CD, PRODUCT_ID),
  KEY IDX_MATERIAL_PRODUCT (COMPANY_CD, MATERIAL_PRODUCT_ID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Danh sách nguyên vật liệu cấu thành sản phẩm';

-- Remove old non-standard names, if an earlier script was installed.
DROP PROCEDURE IF EXISTS getProductMaterialInfo;
DROP PROCEDURE IF EXISTS getProductMaterialSummary;
DROP PROCEDURE IF EXISTS setProductMaterialInfo;
DROP PROCEDURE IF EXISTS delProductMaterialInfo;

DROP PROCEDURE IF EXISTS sp_product_material_info_get;
DROP PROCEDURE IF EXISTS sp_product_material_summary_get;
DROP PROCEDURE IF EXISTS sp_product_material_info_upsert;
DROP PROCEDURE IF EXISTS sp_product_material_info_delete;

DELIMITER $$

CREATE PROCEDURE sp_product_material_info_get(
  IN p_COMPANY_CD VARCHAR(20),
  IN p_PRODUCT_ID BIGINT
)
BEGIN
  SELECT
    m.MATERIAL_ID,
    m.COMPANY_CD,
    m.PRODUCT_ID,
    m.MATERIAL_PRODUCT_ID,
    m.QUANTITY,
    p.PRODUCT_CD AS MATERIAL_PRODUCT_CD,
    p.PRODUCT_NM_VIET AS MATERIAL_PRODUCT_NM,
    u.UNIT_NM
  FROM product_material_info m
  INNER JOIN product_info p
     ON p.PRODUCT_ID = m.MATERIAL_PRODUCT_ID
    AND p.COMPANY_CD = m.COMPANY_CD
    AND IFNULL(p.ISDEL, '0') = '0'
  INNER JOIN product_info finished
     ON finished.PRODUCT_ID = m.PRODUCT_ID
    AND finished.COMPANY_CD = m.COMPANY_CD
    AND IFNULL(finished.ISDEL, '0') = '0'
  LEFT JOIN product_unit u
     ON u.UNIT_ID = p.UNIT_ID
    AND u.COMPANY_CD = p.COMPANY_CD
    AND IFNULL(u.ISDEL, '0') = '0'
  WHERE m.COMPANY_CD = p_COMPANY_CD
    AND m.PRODUCT_ID = p_PRODUCT_ID
    AND IFNULL(m.ISDEL, '0') = '0'
  ORDER BY p.PRODUCT_CD, m.MATERIAL_ID;
END$$

CREATE PROCEDURE sp_product_material_summary_get(
  IN p_COMPANY_CD VARCHAR(20)
)
BEGIN
  SELECT m.PRODUCT_ID, COUNT(*) AS MATERIAL_COUNT
  FROM product_material_info m
  INNER JOIN product_info p
    ON p.PRODUCT_ID = m.MATERIAL_PRODUCT_ID
   AND p.COMPANY_CD = m.COMPANY_CD
   AND IFNULL(p.ISDEL,'0') = '0'
  WHERE m.COMPANY_CD = p_COMPANY_CD
    AND IFNULL(m.ISDEL,'0') = '0'
  GROUP BY m.PRODUCT_ID;
END$$

CREATE PROCEDURE sp_product_material_info_upsert(
  IN p_MATERIAL_ID BIGINT,
  IN p_COMPANY_CD VARCHAR(20),
  IN p_PRODUCT_ID BIGINT,
  IN p_MATERIAL_PRODUCT_ID BIGINT,
  IN p_QUANTITY DECIMAL(18,6),
  IN p_CREATE_BY VARCHAR(20),
  IN p_UPDATE_BY VARCHAR(20)
)
BEGIN
  DECLARE v_MATERIAL_ID BIGINT DEFAULT 0;

  IF p_COMPANY_CD IS NULL OR TRIM(p_COMPANY_CD) = '' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'COMPANY_CD is required';
  END IF;

  IF p_PRODUCT_ID IS NULL OR p_PRODUCT_ID <= 0
     OR p_MATERIAL_PRODUCT_ID IS NULL OR p_MATERIAL_PRODUCT_ID <= 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid product ID';
  END IF;

  IF p_PRODUCT_ID = p_MATERIAL_PRODUCT_ID THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A product cannot use itself as material';
  END IF;

  IF p_QUANTITY IS NULL OR p_QUANTITY <= 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'QUANTITY must be greater than zero';
  END IF;

  IF NOT EXISTS (
      SELECT 1 FROM product_info
      WHERE COMPANY_CD = p_COMPANY_CD
        AND PRODUCT_ID = p_PRODUCT_ID AND IFNULL(ISDEL,'0') = '0'
  ) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Finished product not found in company';
  END IF;

  IF NOT EXISTS (
      SELECT 1 FROM product_info
      WHERE COMPANY_CD = p_COMPANY_CD
        AND PRODUCT_ID = p_MATERIAL_PRODUCT_ID AND IFNULL(ISDEL,'0') = '0'
  ) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Material not found in company';
  END IF;

  IF EXISTS (
      SELECT 1 FROM product_material_info
      WHERE COMPANY_CD = p_COMPANY_CD
        AND PRODUCT_ID = p_PRODUCT_ID
        AND MATERIAL_PRODUCT_ID = p_MATERIAL_PRODUCT_ID
        AND IFNULL(ISDEL,'0') = '0'
        AND MATERIAL_ID <> IFNULL(p_MATERIAL_ID,0)
  ) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Material already exists for this product';
  END IF;

  IF IFNULL(p_MATERIAL_ID,0) = 0 THEN
    INSERT INTO product_material_info (
      COMPANY_CD, PRODUCT_ID, MATERIAL_PRODUCT_ID, QUANTITY,
      ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
    ) VALUES (
      p_COMPANY_CD, p_PRODUCT_ID, p_MATERIAL_PRODUCT_ID, p_QUANTITY,
      '0', p_CREATE_BY, NOW(), p_UPDATE_BY, NOW()
    );
    SET v_MATERIAL_ID = LAST_INSERT_ID();
  ELSE
    IF NOT EXISTS (
        SELECT 1 FROM product_material_info
        WHERE MATERIAL_ID = p_MATERIAL_ID AND COMPANY_CD = p_COMPANY_CD
          AND PRODUCT_ID = p_PRODUCT_ID AND IFNULL(ISDEL,'0') = '0'
    ) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Material row not found for this product/company';
    END IF;

    UPDATE product_material_info
    SET MATERIAL_PRODUCT_ID = p_MATERIAL_PRODUCT_ID,
        QUANTITY = p_QUANTITY,
        UPDATE_BY = p_UPDATE_BY,
        UPDATE_AT = NOW()
    WHERE MATERIAL_ID = p_MATERIAL_ID AND COMPANY_CD = p_COMPANY_CD
      AND PRODUCT_ID = p_PRODUCT_ID AND IFNULL(ISDEL,'0') = '0';
    SET v_MATERIAL_ID = p_MATERIAL_ID;
  END IF;

  SELECT v_MATERIAL_ID AS MATERIAL_ID;
END$$

CREATE PROCEDURE sp_product_material_info_delete(
  IN p_COMPANY_CD VARCHAR(20),
  IN p_MATERIAL_ID BIGINT,
  IN p_UPDATE_BY VARCHAR(20)
)
BEGIN
  UPDATE product_material_info
     SET ISDEL = '1', UPDATE_BY = p_UPDATE_BY, UPDATE_AT = NOW()
   WHERE COMPANY_CD = p_COMPANY_CD
     AND MATERIAL_ID = p_MATERIAL_ID
     AND IFNULL(ISDEL,'0') = '0';

  IF ROW_COUNT() = 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Material row not found in company';
  END IF;
END$$

DELIMITER ;

-- Example (replace with your company and product ID):
-- CALL sp_product_material_summary_get('0001');
-- CALL sp_product_material_info_get('0001', 101);
-- CALL sp_product_material_info_upsert(0, '0001', 101, 202, 2.500000, 'admin');
-- CALL sp_product_material_info_delete('0001', 1, 'admin');
