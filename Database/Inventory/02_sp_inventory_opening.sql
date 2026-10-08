-- Inventory Opening (IRO) stored procedures
-- Deploy to company database (e.g. am_web_001)
-- MySQL 5.6+

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_inventory_opening_get`$$
CREATE PROCEDURE `sp_inventory_opening_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INPUT_ID` BIGINT
)
BEGIN
    SELECT
        i.INPUT_ID,
        i.INPUT_CD,
        i.CHIT_ID AS TRANSFER_ID,
        IFNULL(i.CHIT_CD, '') AS TRANSFER_CD,
        t.TRANSFER_NO,
        t.TRANSFER_YMD,
        i.COMPANY_CD,
        i.PRODUCT_ID,
        IFNULL(i.PRODUCT_CD, '') AS PRODUCT_CD,
        IFNULL(p.PRODUCT_NM_VIET, '') AS PRODUCT_NM_VIET,
        IFNULL(p.PRODUCT_NM_ENG, '') AS PRODUCT_NM_ENG,
        IFNULL(p.PRODUCT_NM_KOR, '') AS PRODUCT_NM_KOR,
        IFNULL(p.PRODUCT_NM_CHINA, '') AS PRODUCT_NM_CHINA,
        i.STORE_ID,
        IFNULL(i.STORE_CD, '') AS STORE_CD,
        IFNULL(s.STORE_NM_VIET, '') AS STORE_NM_VIET,
        IFNULL(s.STORE_NM_ENG, '') AS STORE_NM_ENG,
        IFNULL(s.STORE_NM_KOR, '') AS STORE_NM_KOR,
        IFNULL(s.STORE_NM_CHINA, '') AS STORE_NM_CHINA,
        i.UNIT_ID,
        IFNULL(i.UNIT_CD, '') AS UNIT_CD,
        IFNULL(u.UNIT_NM, '') AS UNIT_NM,
        i.QUANTITY,
        i.UNIT_PRICE_CC,
        i.AMOUNT_CC,
        i.SUMMARY,
        DATE_FORMAT(i.INVENTORY_YMD, '%Y%m%d') AS INVENTORY_YMD,
        IFNULL(i.STATE, '0') AS STATE,
        IFNULL(i.SORT, 0) AS SORT,
        i.CREATE_BY,
        i.CREATE_AT,
        i.UPDATE_BY,
        i.UPDATE_AT
    FROM chit_inventory_input i
    INNER JOIN chit_inventory_transfer t
        ON t.COMPANY_CD = i.COMPANY_CD
       AND t.TRANSFER_ID = i.CHIT_ID
       AND IFNULL(t.ISDEL, '0') = '0'
       AND t.CHIT_TYPE = 'IRO'
       AND t.INPUT_TYPE = 'INV'
    LEFT JOIN product_info p
        ON p.COMPANY_CD = i.COMPANY_CD
       AND p.PRODUCT_ID = i.PRODUCT_ID
       AND IFNULL(p.ISDEL, '0') = '0'
    LEFT JOIN store_info s
        ON s.COMPANY_CD = i.COMPANY_CD
       AND s.STORE_ID = i.STORE_ID
       AND IFNULL(s.ISDEL, '0') = '0'
    LEFT JOIN product_unit u
        ON u.COMPANY_CD = i.COMPANY_CD
       AND u.UNIT_ID = i.UNIT_ID
       AND IFNULL(u.ISDEL, '0') = '0'
    WHERE i.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(i.ISDEL, '0') = '0'
      AND i.CHIT_TYPE = 'IRO'
      AND (p_INPUT_ID IS NULL OR p_INPUT_ID = 0 OR i.INPUT_ID = p_INPUT_ID)
    ORDER BY i.INPUT_ID DESC;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_opening_check_product_store`$$
CREATE PROCEDURE `sp_inventory_opening_check_product_store`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_PRODUCT_ID` BIGINT,
    IN `p_STORE_ID` BIGINT,
    IN `p_EXCLUDE_INPUT_ID` BIGINT
)
BEGIN
    SELECT COUNT(1) AS EXISTS_CNT
    FROM chit_inventory_input
    WHERE COMPANY_CD = p_COMPANY_CD
      AND IFNULL(ISDEL, '0') = '0'
      AND CHIT_TYPE = 'IRO'
      AND PRODUCT_ID = p_PRODUCT_ID
      AND STORE_ID = p_STORE_ID
      AND (p_EXCLUDE_INPUT_ID IS NULL OR p_EXCLUDE_INPUT_ID = 0 OR INPUT_ID <> p_EXCLUDE_INPUT_ID);
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_opening_product_get`$$
CREATE PROCEDURE `sp_inventory_opening_product_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_PRODUCT_ID` BIGINT,
    IN `p_PRODUCT_CD` VARCHAR(20)
)
BEGIN
    SELECT
        p.PRODUCT_ID AS ID,
        p.PRODUCT_CD AS CD,
        p.UNIT_ID,
        IFNULL(u.UNIT_CD, '') AS UNIT_CD
    FROM product_info p
    LEFT JOIN product_unit u
        ON u.COMPANY_CD = p.COMPANY_CD
       AND u.UNIT_ID = p.UNIT_ID
       AND IFNULL(u.ISDEL, '0') = '0'
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(p.ISDEL, '0') = '0'
      AND (
            (p_PRODUCT_ID IS NOT NULL AND p_PRODUCT_ID > 0 AND p.PRODUCT_ID = p_PRODUCT_ID)
         OR (
                (p_PRODUCT_ID IS NULL OR p_PRODUCT_ID = 0)
            AND p_PRODUCT_CD IS NOT NULL
            AND p_PRODUCT_CD <> ''
            AND p.PRODUCT_CD = p_PRODUCT_CD
            )
          )
    LIMIT 1;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_opening_store_get`$$
CREATE PROCEDURE `sp_inventory_opening_store_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_STORE_ID` BIGINT,
    IN `p_STORE_CD` VARCHAR(20)
)
BEGIN
    SELECT
        s.STORE_ID AS ID,
        s.STORE_CD AS CD,
        CAST(NULL AS SIGNED) AS UNIT_ID,
        CAST(NULL AS CHAR) AS UNIT_CD
    FROM store_info s
    WHERE s.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(s.ISDEL, '0') = '0'
      AND (
            (p_STORE_ID IS NOT NULL AND p_STORE_ID > 0 AND s.STORE_ID = p_STORE_ID)
         OR (
                (p_STORE_ID IS NULL OR p_STORE_ID = 0)
            AND p_STORE_CD IS NOT NULL
            AND p_STORE_CD <> ''
            AND s.STORE_CD = p_STORE_CD
            )
          )
    LIMIT 1;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_opening_unit_get`$$
CREATE PROCEDURE `sp_inventory_opening_unit_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_UNIT_ID` BIGINT,
    IN `p_UNIT_CD` VARCHAR(20)
)
BEGIN
    SELECT
        u.UNIT_ID AS ID,
        u.UNIT_CD AS CD,
        u.UNIT_ID AS UNIT_ID,
        u.UNIT_CD AS UNIT_CD
    FROM product_unit u
    WHERE u.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(u.ISDEL, '0') = '0'
      AND (
            (p_UNIT_ID IS NOT NULL AND p_UNIT_ID > 0 AND u.UNIT_ID = p_UNIT_ID)
         OR (
                (p_UNIT_ID IS NULL OR p_UNIT_ID = 0)
            AND p_UNIT_CD IS NOT NULL
            AND p_UNIT_CD <> ''
            AND u.UNIT_CD = p_UNIT_CD
            )
          )
    LIMIT 1;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_opening_transfer_get`$$
CREATE PROCEDURE `sp_inventory_opening_transfer_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_TRANSFER_ID` BIGINT
)
BEGIN
    SELECT
        TRANSFER_ID,
        TRANSFER_CD,
        TRANSFER_NO,
        TRANSFER_YMD,
        REMARK,
        SORT
    FROM chit_inventory_transfer
    WHERE COMPANY_CD = p_COMPANY_CD
      AND TRANSFER_ID = p_TRANSFER_ID
      AND IFNULL(ISDEL, '0') = '0'
      AND CHIT_TYPE = 'IRO'
      AND INPUT_TYPE = 'INV'
    LIMIT 1;
END$$

-- PDF / report viewer (DEFAULT_GRID) — same data shape as list + ROW_NO
DROP PROCEDURE IF EXISTS `rpt_inventory_opening_info`$$
CREATE PROCEDURE `rpt_inventory_opening_info`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INPUT_ID` BIGINT,
    IN `p_LANGUAGE` VARCHAR(20)
)
BEGIN
    SET @rownum := 0;

    SELECT
        (@rownum := @rownum + 1) AS ROW_NO,
        i.INPUT_ID,
        i.INPUT_CD,
        i.CHIT_ID AS TRANSFER_ID,
        IFNULL(i.CHIT_CD, '') AS TRANSFER_CD,
        t.TRANSFER_NO,
        t.TRANSFER_YMD,
        i.COMPANY_CD,
        i.PRODUCT_ID,
        IFNULL(i.PRODUCT_CD, '') AS PRODUCT_CD,
        IFNULL(p.PRODUCT_NM_VIET, '') AS PRODUCT_NM_VIET,
        IFNULL(p.PRODUCT_NM_ENG, '') AS PRODUCT_NM_ENG,
        IFNULL(p.PRODUCT_NM_KOR, '') AS PRODUCT_NM_KOR,
        IFNULL(p.PRODUCT_NM_CHINA, '') AS PRODUCT_NM_CHINA,
        i.STORE_ID,
        IFNULL(i.STORE_CD, '') AS STORE_CD,
        IFNULL(s.STORE_NM_VIET, '') AS STORE_NM_VIET,
        IFNULL(s.STORE_NM_ENG, '') AS STORE_NM_ENG,
        IFNULL(s.STORE_NM_KOR, '') AS STORE_NM_KOR,
        IFNULL(s.STORE_NM_CHINA, '') AS STORE_NM_CHINA,
        i.UNIT_ID,
        IFNULL(i.UNIT_CD, '') AS UNIT_CD,
        IFNULL(u.UNIT_NM, '') AS UNIT_NM,
        i.QUANTITY,
        i.UNIT_PRICE_CC,
        i.AMOUNT_CC,
        i.SUMMARY,
        DATE_FORMAT(i.INVENTORY_YMD, '%Y%m%d') AS INVENTORY_YMD,
        IFNULL(i.STATE, '0') AS STATE,
        IFNULL(i.SORT, 0) AS SORT
    FROM chit_inventory_input i
    INNER JOIN chit_inventory_transfer t
        ON t.COMPANY_CD = i.COMPANY_CD
       AND t.TRANSFER_ID = i.CHIT_ID
       AND IFNULL(t.ISDEL, '0') = '0'
       AND t.CHIT_TYPE = 'IRO'
       AND t.INPUT_TYPE = 'INV'
    LEFT JOIN product_info p
        ON p.COMPANY_CD = i.COMPANY_CD
       AND p.PRODUCT_ID = i.PRODUCT_ID
       AND IFNULL(p.ISDEL, '0') = '0'
    LEFT JOIN store_info s
        ON s.COMPANY_CD = i.COMPANY_CD
       AND s.STORE_ID = i.STORE_ID
       AND IFNULL(s.ISDEL, '0') = '0'
    LEFT JOIN product_unit u
        ON u.COMPANY_CD = i.COMPANY_CD
       AND u.UNIT_ID = i.UNIT_ID
       AND IFNULL(u.ISDEL, '0') = '0'
    WHERE i.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(i.ISDEL, '0') = '0'
      AND i.CHIT_TYPE = 'IRO'
      AND (p_INPUT_ID IS NULL OR p_INPUT_ID = 0 OR i.INPUT_ID = p_INPUT_ID)
    ORDER BY i.INPUT_ID DESC;
END$$

DELIMITER ;
